using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Broadcast.Shared;

namespace Broadcast.Server
{
    public class Server
    {
        const ushort MAX_LOBBIES_PER_QUERY = 200;
        const byte VERSION = Networking.VERSION;
        const ushort SECONDS_BEFORE_CLEANUP = 30;

        private delegate void ControllerHandlerDelegate(byte[] data, TcpClient client, uint clientId);

        private readonly Dictionary<byte, ControllerHandlerDelegate> controllers = new Dictionary<byte, ControllerHandlerDelegate>();

        private readonly HashSet<uint> clientIDs = new HashSet<uint>();
        private readonly List<Lobby> lobbies = new List<Lobby>();
        private readonly Dictionary<Lobby, DateTime> lastHeardAbout = new Dictionary<Lobby, DateTime>();
        private readonly Dictionary<TcpClient, byte[]> pendingPunchRequests = new Dictionary<TcpClient, byte[]>();
        private readonly Logger logger;
        private readonly RNGCryptoServiceProvider random;


        public Server()
        {
            controllers.Add(Networking.PROTOCOL_SUBMIT, HandleSubmit);
            controllers.Add(Networking.PROTOCOL_QUERY, HandleQuery);
            controllers.Add(Networking.PROTOCOL_DELETE, HandleDelete);
            controllers.Add(Networking.PROTOCOL_HELLO, HandleHello);
            controllers.Add(Networking.PROTOCOL_REQUEST_PUNCH_TO_LOBBY, HandlePunch);

            logger = new Logger(programName: "B_Server", outputToFile: true, addDateSuffix: true);

            random = new RNGCryptoServiceProvider();

            TcpListener server = new TcpListener(IPAddress.Any, Networking.PORT);


            server.Start();
            logger.Info($"Broadcast ready - Port {Networking.PORT} - Version {VERSION}");

            while (true)   //we wait for a connection
            {
                TcpClient client = server.AcceptTcpClient();  //if a connection exists, the server will accept it

                uint clientId = 0;
                while (clientId == 0 || clientIDs.Contains(clientId)) {
                    clientId = (uint)new Random().Next(int.MaxValue);
                }

                lock (clientIDs) {
                    clientIDs.Add(clientId);
                }

                logger.Info($"Client [{clientId}] just connected (ENTER)");

                Task.Run(() => HandleClientMessages(clientId, client));
            }
        }

        private void HandleMessage(byte[] msgBuffer, uint clientId, TcpClient client)
        {
            if (msgBuffer.Length <= 0) {
                return;
            }

            var messageType = msgBuffer[0];
            byte[] deserializable = new byte[msgBuffer.Length - 1];
            Array.Copy(msgBuffer, 1, deserializable, 0, deserializable.Length);

            if (controllers.TryGetValue(messageType, out var controller)) {
                logger.Trace($"Client [{clientId}] sent (MessageType:{messageType}) [Length: {deserializable.Length} bytes]");
                controller(deserializable, client, clientId);
            }
            else {
                // ???
                logger.Warn("Client [" + clientId + "] sent an unknown message type: (MessageType:" + messageType + ") is not implemented in this broadcast version");
            }
        }


        private void HandleClientMessages(uint clientId, TcpClient client)
        {
            while (true) {
                try {
                    var stream = client.GetStream();
                    // This should block eternally, unless there is an error
                    stream.ReadForever((bytes) =>
                    {
                        HandleMessage(bytes, clientId, client);
                        return true; // Keep listening
                    });
                }
                catch (IOException e) {
                    if (e?.InnerException is SocketException socketException && socketException.SocketErrorCode == SocketError.TimedOut) {
                        logger.Info("Client [" + clientId + "] is no longer connected (TIME OUT)");
                    }
                    else {
                        logger.Info("Client [" + clientId + "] triggered an IOException (see debug)");
                        logger.Debug(e.ToString());
                    }
                }
                catch (SocketException e) {
                    logger.Info("Client [" + clientId + "] triggered a SocketException (see debug)");
                    logger.Debug(e.ToString());
                }
                catch (TaskCanceledException e) {
                    logger.Trace("Caught a task cancellation - probably normal, going to rethrow the cancellation to end the task. Trace below:\n" + e.ToString());
                    break;
                }
                catch (Exception e) {
                    logger.Error("UNCAUGHT EXCEPTION IN CLIENT TASK for Client [{0}] ! Ending the task. Trace below:".Format(clientId));
                    logger.Error(e.ToString());
                    break;
                }
            }

            lock (clientIDs) {
                clientIDs.Remove(clientId);
            }
        }

        void CleanLobbies()
        {
            logger.Trace("Cleaning lobbies up...");
            int removed = 0;

            var lobbyArray = lobbies.ToArray();
            foreach (var lobby in lobbyArray) {

                if (!lastHeardAbout.TryGetValue(lobby, out var lastTime)) {
                    continue;
                }

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

            byte[] byteMsg;
            bool isRequestingPunch = false;

            if (pendingPunchRequests.TryGetValue(client, out byteMsg)) {

                lock (pendingPunchRequests) {
                    pendingPunchRequests.Remove(client);
                }

                isRequestingPunch = true;
            }
            else {
                byteMsg = helloMsg.PrefixWith(Networking.PROTOCOL_HELLO);
            }

            client.GetStream().WriteData(byteMsg);

            if (isRequestingPunch) {
                logger.Info("Sent a PUNCH request client [{0}] ({1} bytes)".Format(clientId, byteMsg.Length));
            }
            else {
                logger.Info("Sent a HELLO to client [{0}] ({1} bytes)".Format(clientId, byteMsg.Length));
            }
        }

        void HandleQuery(byte[] deserializable, TcpClient client, uint clientId)
        {
            CleanLobbies();
            Query query = Query.Deserialize(deserializable);

            logger.Debug("Preparing to send a lobby list to client [{0}]".Format(clientId));

            var results = lobbies.FindAll(
                o =>
                {
                    if (
                        (query.title.Length == 0 || o.title.ToLower().Contains(query.title.ToLower())) &&
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
            if (results.Count > MAX_LOBBIES_PER_QUERY) {
                results.RemoveRange(MAX_LOBBIES_PER_QUERY, results.Count - MAX_LOBBIES_PER_QUERY);
            }

            var listBuf = Lobby.SerializeList(lobbies);
            client.GetStream().WriteData(listBuf.PrefixWith(Networking.PROTOCOL_QUERY_RESPONSE));

            logger.Info($"Sent a lobby list ({results.Count} valid lobbies out of {lobbies.Count}) to client [{clientId}]! ({listBuf.Length} bytes)");
        }

        void HandleSubmit(byte[] deserializable, TcpClient client, uint clientId)
        {
            logger.Debug("Preparing to decode a lobby submission from client [{0}]".Format(clientId));

            Lobby lobby;
            lobby = Lobby.Deserialize(deserializable);

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
                uIntId = GenerateLobbyID();
                lobby.id = uIntId;
                lobbies.Add(lobby);
                logger.Debug("Created new lobby (ID: " + uIntId + ") with addresses [" + lobby.strAddress + "] (IPV4: " + string.Join(".", lobby.address) + ")");
            }

            lock (lastHeardAbout) {
                lastHeardAbout[lobby] = DateTime.UtcNow;
            }

            client.GetStream().WriteData(BitConverter.GetBytes(uIntId).PrefixWith(Networking.PROTOCOL_SUBMISSION_RESPONSE)); // I return the ID of the lobby
            lobby.SetOwner(client);

            logger.Info("Finished processing lobby submission from client [{0}] (gave them ID {1} for their lobby)!".Format(clientId, uIntId));
        }

        void HandleDelete(byte[] deserializable, TcpClient client, uint clientId)
        {
            logger.Debug("Preparing to remove a lobby requested by client [{0}]".Format(clientId));

            var targetLobbyId = BitConverter.ToUInt32(deserializable, 0);
            var targetLobby = lobbies.Find(o => o.id == targetLobbyId);

            if (targetLobby == null) {
                logger.Info("Client [{0}] tried to remove lobby {1} but it does not exist. Doing nothing.".Format(clientId, targetLobbyId));
                return;
            }

            if (targetLobby.GetOwner() != client) {
                logger.Info("Client [{0}] tried to remove lobby {1} but they're not the owner. Doing nothing.".Format(clientId, targetLobbyId));
                return;
            }

            lock (lobbies) {
                var removed = lobbies.RemoveAll(o => o.id == targetLobby.id);
                logger.Debug("Removed {1} lobbies (every lobby with ID {2}) as requested by client [{0}]".Format(clientId, removed, targetLobbyId));
            }
            logger.Info("Finished removing lobby {1} as requested by client [{0}]".Format(clientId, targetLobbyId));
        }

        void HandlePunch(byte[] deserializable, TcpClient client, uint clientId)
        {
            logger.Debug("Preparing to punch a lobby for client [{0}]".Format(clientId));

            var targetLobby = BitConverter.ToUInt32(deserializable, 0);
            var lobby = lobbies.Find(o => o.id == targetLobby);

            if (lobby == null) {
                logger.Warn("Asked to punch a NON-EXISTENT lobby {1} as requested by client [{0}]. Doing nothing.".Format(clientId, targetLobby));
                return;
            }

            var portBytes = BitConverter.GetBytes(lobby.port);

            var endpoint = (IPEndPoint)lobby.GetOwner().Client.RemoteEndPoint;
            var originBytes = ((IPEndPoint)client.Client.RemoteEndPoint).Address.GetAddressBytes();

            lock (pendingPunchRequests) {
                pendingPunchRequests[lobby.GetOwner()] = new byte[]
                    {
                        Networking.PROTOCOL_HELLO_PLEASE_PUNCH,
                        originBytes[0],
                        originBytes[1],
                        originBytes[2],
                        originBytes[3],
                        portBytes[0],
                        portBytes[1]
                    };
            }

            logger.Info("Added pending PUNCH paquet for {3} towards {0}:{1} (the owner of lobby {2})".Format(string.Join(".", originBytes), lobby.port, lobby.id, endpoint.Address));
        }

        private uint GenerateLobbyID()
        {
            uint id = 0;
            while (id == 0 || lobbies.Find(o => o.id == id) != null) {
                id = GetRandomUInt();
            }

            return id;
        }

        private uint GetRandomUInt()
        {
            byte[] result = new byte[sizeof(int)];

            random.GetBytes(result);

            return BitConverter.ToUInt32(result, 0);
        }
    }
}
