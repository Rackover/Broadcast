using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Broadcast.Shared;

namespace Broadcast.Server
{
    public class Server
    {
        const ushort RESPONSE_SIZE = 200;
        const byte VERSION = Networking.VERSION;
        const ushort SECONDS_BEFORE_CLEANUP = 30;
        const ushort SECONDS_BEFORE_EMPTY_READ = 20;

        List<Lobby> lobbies = new List<Lobby>();
        Dictionary<Lobby, DateTime> lastHeardAbout = new Dictionary<Lobby, DateTime>();
        Logger logger;

        Dictionary<TcpClient, byte[]> pendingPunchRequests = new Dictionary<TcpClient, byte[]>();

        public Server()
        {
            logger = new Logger(programName:"B_Server", outputToFile:true);

            TcpListener server = new TcpListener(IPAddress.Any, Networking.PORT);
            
            var controller = new Dictionary<byte, Action<byte[], TcpClient, uint>>() {
                {Networking.PROTOCOL_SUBMIT, HandleSubmit },
                {Networking.PROTOCOL_QUERY, HandleQuery },
                {Networking.PROTOCOL_DELETE, HandleDelete },
                {Networking.PROTOCOL_HELLO, HandleHello },
                {Networking.PROTOCOL_PUNCH, HandlePunch }
            };


            server.Start();  // this will start the server
            logger.Info("Broadcast ready - Port " + Networking.PORT + " - Version " + VERSION);
            while (true)   //we wait for a connection
            {
                TcpClient client = server.AcceptTcpClient();  //if a connection exists, the server will accept it
                uint clientId = (uint)new Random().Next(int.MaxValue);
                logger.Info("Client ["+clientId+"] just connected (ENTER)");

                new Task(delegate {
                    NetworkStream ns = client.GetStream();
                    ns.ReadTimeout = SECONDS_BEFORE_EMPTY_READ * 1000;
                    while (client.Connected)  //while the client is connected, we look for incoming messages
                    {
                        try {
                            // BLOCKing
                            var msgBuffer = ns.Read();
                            if (client.Available <= 0 && msgBuffer.Length == 0) {
                                break;
                            }

                            var messageType = msgBuffer[0];
                            byte[] deserializable = new byte[msgBuffer.Length - 1];
                            Array.Copy(msgBuffer, 1, deserializable, 0, deserializable.Length);

                            if (controller.ContainsKey(messageType)) {
                                logger.Trace("Client [{0}] sent (MessageType:{1}) [Length: {2} bytes]", clientId, messageType, deserializable.Length);
                                controller[messageType](deserializable, client, clientId);
                            }
                            else {
                                // ???
                                logger.Warn("Client [" + clientId + "] sent an unknown message type: (MessageType:" + messageType+") is not implemented in this broadcast version");
                            }
                        }
                        catch (IOException e) {
                            logger.Info("Client ["+clientId+"] triggered an IOException (see debug)");
                            logger.Debug(e.ToString());
                            break;
                        }
                        catch (SocketException e) {
                            logger.Info("Client [" + clientId + "] triggered a SocketException (see debug)");
                            logger.Debug(e.ToString());
                            break;
                        }
                        catch (TaskCanceledException e) {
                            logger.Trace("Caught a task cancellation - probably normal, going to rethrow the cancellation to end the task. Trace below:\n"+e.ToString());
                            throw new TaskCanceledException(e.ToString()); // Passthrough
                        }
                        catch (Exception e) {
                            logger.Error("UNCAUGHT EXCEPTION IN CLIENT TASK for Client [{0}] ! Ending the task. Trace below:".Format(clientId));
                            logger.Error(e.ToString());
                            throw new TaskCanceledException();
                        }
                    }
                    ns.Dispose();
                    client.Close();
                    logger.Info("Client ["+clientId+"] is no longer connected (EXIT)");
                }).Start();


                CleanLobbies();
            }
        }


        void CleanLobbies()
        {
            logger.Trace("Cleaning lobbies up...");
            int removed = 0;
            foreach (var lobby in lobbies.ToArray()) {
                if (!lastHeardAbout.ContainsKey(lobby)) {
                    continue;
                }
                var lastTime = lastHeardAbout[lobby];
                if (DateTime.UtcNow.Subtract(lastTime).TotalSeconds > SECONDS_BEFORE_CLEANUP) {
                    lock (lobbies) {
                        lobbies.RemoveAll(o => o.id == lobby.id);
                        removed++;
                    }
                }
            }
            if (removed > 0) logger.Debug("Cleanup finished, removed " + removed + " lobbies");
            logger.Trace("Done cleaning lobbies up");
        }

        void HandleHello(byte[] _, TcpClient client, uint clientId)
        {
            var helloMsg = Encoding.UTF8.GetBytes("Hello!");
            var msg = new List<byte>() { Networking.PROTOCOL_HELLO};
            msg.AddRange(helloMsg);

            var byteMsg = msg.ToArray();
            bool isRequestingPunch = false;

            if (pendingPunchRequests.ContainsKey(client))
            {
                byteMsg = pendingPunchRequests[client];
                pendingPunchRequests.Remove(client);
                isRequestingPunch = true;
            }

            client.GetStream().WriteData(byteMsg);

            if (isRequestingPunch)
            {
                logger.Info("Sent a PUNCH request client [{0}] ({1} bytes)".Format(clientId, byteMsg.Length));
            }
            else
            {
                logger.Info("Sent a HELLO to client [{0}] ({1} bytes)".Format(clientId, byteMsg.Length));
            }
        }

        void HandleQuery(byte[] deserializable, TcpClient client, uint clientId)
        {
            CleanLobbies();
            Query query = Query.Deserialize(deserializable);

            logger.Debug("Preparing to send a lobby list to client [{0}]".Format(clientId));

            var results = lobbies.FindAll(
                o => {
                    if (
                        (!query.freeSpotsOnly || o.maxPlayers < o.players) &&
                        (!query.officialOnly || o.isOfficial == true) &&
                        (!query.publicOnly || o.isPrivate == false) &&
                        (!query.strictVersion || o.gameVersion == query.gameVersion) &&
                            query.game == o.game
                        ) {
                        return true;
                    }
                    return false;
                }
            );
            if (results.Count > RESPONSE_SIZE) {
                results.RemoveRange(RESPONSE_SIZE, results.Count - RESPONSE_SIZE);
            }

            var listBuf = Lobby.SerializeList(lobbies);
            client.GetStream().WriteData(listBuf);
            logger.Info("Sent a lobby list ({1} lobbies) to client [{0}]! ({2} bytes)".Format(clientId, lobbies.Count, listBuf.Length));
        }

        void HandleSubmit(byte[] deserializable, TcpClient client, uint clientId)
        {
            logger.Debug("Preparing to decode a lobby submission from client [{0}]".Format(clientId));

            Lobby lobby;
            lobby = Lobby.Deserialize(deserializable);

            var secretKey = Environment.GetEnvironmentVariable("BROADCAST_GAME_KEY_"+lobby.game);
            if (secretKey != null && secretKey == lobby.secretKey) {
                // Forbidden
                return;
            }

            // Autofill address
            if (!lobby.HasAddress()) {
                lobby.strAddress = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
                lobby.address = ((IPEndPoint)client.Client.RemoteEndPoint).Address.GetIPV4Addr();
                logger.Debug("Auto-filled lobby submission address with v4:{1}/v6:{2} for client [{0}]".Format(clientId, string.Join(".", lobby.address), lobby.strAddress));
            }

            var index = lobbies.FindIndex(o => 
                o.GetOwner() == client || 
                o.id == lobby.id || 
                (o.port == lobby.port && o.address.IsSameAs(lobby.address) && o.strAddress == lobby.strAddress)
            );

            uint uIntId = 0;
            if (index > -1) {
                uIntId = lobbies[index].id;
                lobby.id = uIntId;
                lobbies[index] = lobby;
            }
            else {
                uIntId = Convert.ToUInt32(Math.Floor(new Random().NextDouble() * (uint.MaxValue - 1)) + 1);
                lobby.id = uIntId;
                lobbies.Add(lobby);
				logger.Debug("Created new lobby (ID: "+uIntId+") with addresses ["+lobby.strAddress+"] (IPV4: "+string.Join(".", lobby.address)+")");
            }

            lastHeardAbout[lobby] = DateTime.UtcNow;
            var id = BitConverter.GetBytes(uIntId);
            client.GetStream().WriteData(id); // I return the ID of the lobby
            lobby.SetOwner(client);

            logger.Info("Finished processing lobby submission from client [{0}] (gave them ID {1} for their lobby)!".Format(clientId, uIntId));
        }

        void HandleDelete(byte[] deserializable, TcpClient client, uint clientId)
        {
            logger.Debug("Preparing to remove a lobby requested by client [{0}]".Format(clientId));

            var targetLobbyId = BitConverter.ToUInt32(deserializable, 0);
            var targetLobby = lobbies.Find(o => o.id == targetLobbyId);

            if (targetLobby == null)
            {
                logger.Info("Client [{0}] tried to remove lobby {1} but it does not exist. Doing nothing.".Format(clientId, targetLobbyId));
                return;
            }

            if (targetLobby.GetOwner() != client)
            {
                logger.Info("Client [{0}] tried to remove lobby {1} but they're not the owner. Doing nothing.".Format(clientId, targetLobbyId));
                return;
            }

            lock (lobbies) {
                var removed = lobbies.RemoveAll(o => o.id == targetLobby.id);
                logger.Debug("Removed {1} lobbies (every lobby with ID {2}) as requested by client [{0}]".Format(clientId, removed, targetLobbyId)) ;
            }
            logger.Info("Finished removing lobby {1} as requested by client [{0}]".Format(clientId, targetLobbyId));
        }

        void HandlePunch(byte[] deserializable, TcpClient client, uint clientId)
        {
            logger.Debug("Preparing to punch a lobby for client [{0}]".Format(clientId));

            var targetLobby = BitConverter.ToUInt32(deserializable, 0);
            var lobby = lobbies.Find(o => o.id == targetLobby);

            if (lobby == null)
            {
                logger.Warn("Asked to punch a NON-EXISTENT lobby {1} as requested by client [{0}]. Doing nothing.".Format(clientId, targetLobby));
                return;
            }

            var portBytes = BitConverter.GetBytes(lobby.port);

            var endpoint = ((IPEndPoint)lobby.GetOwner().Client.RemoteEndPoint);
            var originBytes = ((IPEndPoint)client.Client.RemoteEndPoint).Address.GetAddressBytes();

            pendingPunchRequests[lobby.GetOwner()] = new byte[]
                    {
                    Networking.PROTOCOL_PUNCH,
                    originBytes[0],
                    originBytes[1],
                    originBytes[2],
                    originBytes[3],
                    portBytes[0],
                    portBytes[1]
                    };

            logger.Info("Added pending PUNCH paquet for {3} towards {0}:{1} (the owner of lobby {2})".Format(string.Join(".", originBytes), lobby.port, lobby.id, endpoint.Address));
        }
    }
}
