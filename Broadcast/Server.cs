using System;
using System.Collections.Generic;
using System.IO;
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

        public Server()
        {
            logger = new Logger(programName:"B_Server", outputToFile:true);

            TcpListener server = new TcpListener(IPAddress.Any, Networking.PORT);
            
            var controller = new Dictionary<byte, Action<byte[], TcpClient>>() {
                {Networking.PROTOCOL_SUBMIT, HandleSubmit },
                {Networking.PROTOCOL_QUERY, HandleQuery },
                {Networking.PROTOCOL_DELETE, HandleDelete },
                {Networking.PROTOCOL_HELLO, HandleHello }
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
                            var lengthBuffer = new byte[4]; // uint is 4 bytes

                            // BLOCKing
                            var msgBuffer = ns.Read();
                            if (msgBuffer.Length == 0) continue;

                            var messageType = msgBuffer[0];
                            byte[] deserializable = new byte[msgBuffer.Length - 1];
                            Array.Copy(msgBuffer, 1, deserializable, 0, deserializable.Length);

                            if (controller.ContainsKey(messageType)) {
                                logger.Trace("Client [{0}] sent (MessageType:{1}) [Length: {2} bytes]", clientId, messageType, deserializable.Length);
                                controller[messageType](deserializable, client);
                            }
                            else {
                                // ???
                                logger.Warn("Client [" + clientId + "] sent an unknown message type: (MessageType:" + messageType+") is not implemented in this broadcast version");
                            }
                        }
                        catch (IOException e) {
                            logger.Info("Client ["+clientId+"] had an IOException (see debug)");
                            logger.Debug(e.ToString());
                            break;
                        }
                        catch (SocketException e) {
                            logger.Info("Client [" + clientId + "] had an SocketException (see debug)");
                            logger.Debug(e.ToString());
                            break;
                        }
                    }
                    client.Dispose();
                    logger.Info("Client ["+clientId+"] is no longer connected (EXIT)");
                }).Start();


                CleanLobbies();
            }
        }


        void CleanLobbies()
        {
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
        }

        void HandleHello(byte[] _, TcpClient client)
        {
            client.GetStream().WriteData(Encoding.UTF8.GetBytes("Hello!"));
        }

        void HandleQuery(byte[] deserializable, TcpClient client)
        {
            CleanLobbies();
            Query query = Query.Deserialize(deserializable);

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

            client.GetStream().WriteData(Lobby.SerializeList(lobbies));
        }

        void HandleSubmit(byte[] deserializable, TcpClient client)
        {
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
            }

            var index = lobbies.FindIndex(o => o.id == lobby.id || (o.port == lobby.port && o.address.IsSameAs(lobby.address) && o.strAddress == lobby.strAddress));
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
				logger.Info("Created new lobby (ID: "+uIntId+") with addresses ["+lobby.strAddress+"] (IPV4: "+string.Join(".", lobby.address)+")");
            }

            lastHeardAbout[lobby] = DateTime.UtcNow;
            var id = BitConverter.GetBytes(uIntId);
            Array.Reverse(id);
            client.GetStream().WriteData(id); // I return the ID of the lobby
        }

        void HandleDelete(byte[] deserializable, TcpClient client)
        {
            var targetLobby = BitConverter.ToUInt32(deserializable, 0);
            lock (lobbies) {
                lobbies.RemoveAll(o => o.id == targetLobby);
            }
        }
    }
}
