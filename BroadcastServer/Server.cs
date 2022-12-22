namespace Broadcast.Server
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Broadcast.Shared;

    public class Server
    {
        const ushort MAX_LOBBIES_PER_QUERY = 200;
        const ushort UPDATE_KILL_LIST_EVERY_SECOND = 10;
        const ushort REPORT_EVERY_SECOND = 30;
        const byte VERSION = Networking.VERSION;
        const ushort SECONDS_BEFORE_CLEANUP = 5;
        const string KILL_LIST_NAME = "KILL.TXT";

        private struct Client
        {
            public readonly uint id;
            public readonly Thread thread;

            public Client(uint id, Thread thread)
            {
                this.id = id;
                this.thread = thread;
            }
        }

        private delegate void ControllerHandlerDelegate(byte[] data, TcpClient client, uint clientId);

        private readonly Dictionary<byte, ControllerHandlerDelegate> controllers = new Dictionary<byte, ControllerHandlerDelegate>();

        private readonly List<Lobby> lobbies = new List<Lobby>();
        private readonly Dictionary<Lobby, DateTime> lastHeardAbout = new Dictionary<Lobby, DateTime>();
        private readonly Dictionary<TcpClient, byte[]> pendingPunchRequests = new Dictionary<TcpClient, byte[]>();
        private readonly Logger logger;
        private readonly RandomNumberGenerator random;
        private readonly HashSet<string> killList = new HashSet<string>();

        private readonly Thread tickingThread;
        private readonly List<Client> clients = new List<Client>(8);

        public Server()
        {
            controllers.Add(Networking.PROTOCOL_SUBMIT, HandleSubmit);
            controllers.Add(Networking.PROTOCOL_QUERY, HandleQuery);
            controllers.Add(Networking.PROTOCOL_DELETE, HandleDelete);
            controllers.Add(Networking.PROTOCOL_HELLO, HandleHello);
            controllers.Add(Networking.PROTOCOL_REQUEST_PUNCH_TO_LOBBY, HandlePunch);

            logger = new Logger(programName: "B_Server", outputToFile: true, addDateSuffix: true);

            random = RandomNumberGenerator.Create();

            TcpListener server = new TcpListener(IPAddress.Any, Networking.PORT);


            server.Start();
            logger.Info($"Broadcast ready - Port {Networking.PORT} - Version {VERSION}");

            // Kill list update
            tickingThread = new Thread(Tick);
            tickingThread.Name = "Ticker";
            tickingThread.Start();

            AcceptClients(server);
        }

        private void AcceptClients(TcpListener server)
        {
            while (true)   //we wait for a connection
            {
                logger.Debug($"Waiting for the next TCP client...");
                TcpClient client = server.AcceptTcpClient();  //if a connection exists, the server will accept it

                string addr = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
                logger.Trace($"Accepted connection from client with addr {addr}");

                UpdateKillList();

                lock (killList) {
                    if (killList.Contains(addr)) {
                        logger.Info($"Client with addr {addr} is on the kill list, killing connection");
                        client.Dispose();
                        continue;
                    }
                }

                uint clientId = 0;
                while (clientId == 0) {
                    clientId = (uint)new Random().Next(int.MaxValue);

                    lock (clients) {
                        for (int i = 0; i < clients.Count; i++) {
                            if (clients[i].id == clientId) {
                                clientId = 0;
                                break;
                            }
                        }
                    }
                }


                logger.Info($"Client [{clientId}] just connected (ENTER)");

                var thread =  new Thread(() => HandleClientMessages(clientId, client));
                thread.Name = $"Client thread (client {clientId})";
                lock (clients) {
                    clients.Add(new Client(clientId, thread));
                }

                thread.Start();
            }
        }

        private void Tick()
        {
            int lastReportSecondsAgo = 0;
            int lastKillListUpdateSecondsAgo = 0;

            if (Thread.CurrentThread != tickingThread) {
                logger.Error($"Wrong thread for tick");
                throw new Exception($"Attempted ticking on another thread that the ticking thread");
            }

            while (true) {

                if (lastKillListUpdateSecondsAgo % UPDATE_KILL_LIST_EVERY_SECOND == 0) {

                    lastKillListUpdateSecondsAgo = 0;

                    try {
                        UpdateKillList();
                    }
                    catch (Exception ex) {
                        logger.Error($"While updating kill list:{ex}");
                    }
                }

                if (lastReportSecondsAgo % UPDATE_KILL_LIST_EVERY_SECOND == 0) {
                    lastReportSecondsAgo = 0;
                    try {
                        int threadCount = Process.GetCurrentProcess().Threads.Count;
                        logger.Info($"Status: {clients.Count} clients registered / {lobbies.Count} lobbies / {killList.Count} kill entries / {pendingPunchRequests.Count} pending punches / {threadCount} threads");
                    }
                    catch (Exception ex) {
                        logger.Error($"In status report: {ex}");
                    }
                }

                Thread.Sleep(1000); // 1 second
                lastReportSecondsAgo++;
                lastKillListUpdateSecondsAgo++;
            }
        }

        private void UpdateKillList()
        {
            lock (killList) {
                killList.Clear();
                if (File.Exists(KILL_LIST_NAME)) {
                    try {
                        using (FileStream fs = File.OpenRead(KILL_LIST_NAME)) {
                            using (StreamReader sr = new StreamReader(fs)) {
                                var line = sr.ReadLine().Trim();
                                if (!killList.Contains(line)) {
                                    killList.Add(line);
                                }
                            }
                        }
                    }
                    catch (Exception e) {
                        logger.Error($"Error while accessing kill list at {KILL_LIST_NAME}:\n{e}");
                    }
                }
            }
        }

        private void HandleMessage(byte[] msgBuffer, uint clientId, TcpClient client)
        {
            if (msgBuffer.Length <= 0) {
                return;
            }

            string addr = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
            lock (killList) {
                if (killList.Contains(addr)) {
                    logger.Info($"Client with addr {addr} is on the kill list, killing connection");
                    client.Dispose();
                    return;
                }
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
                    stream.ReadTimeout = Broadcast.Shared.Networking.TIMEOUT_MS;

                    // This should block eternally, unless there is an error
                    stream.ReadForever((bytes) =>
                    {
                        HandleMessage(bytes, clientId, client);
                        return true; // Keep listening
                    });
                }
                catch (IOException e) {
                    if (e?.InnerException is SocketException socketException) {
                        if (socketException.SocketErrorCode == SocketError.TimedOut) {
                            logger.Info("Client [" + clientId + "] is no longer connected (TIME OUT)");
                        }
                        else if (socketException.SocketErrorCode == SocketError.ConnectionReset) {
                            logger.Info("Client [" + clientId + "] is no longer connected (EXIT)");
                        }
                        else {
                            logger.Info($"Client [{clientId}] triggered an IOException with socket error code {socketException.SocketErrorCode}");
                        }
                    }
                    else {
                        logger.Info("Client [" + clientId + "] triggered an IOException (see debug)");
                        logger.Debug(e.ToString());
                    }

                    break;
                }
                catch (SocketException e) {
                    logger.Info("Client [" + clientId + "] triggered a SocketException (see debug)");
                    logger.Debug(e.ToString());

                    break;
                }
                catch (TaskCanceledException e) {
                    logger.Trace("Caught a task cancellation - probably normal, trace below:\n" + e.ToString());
                    break;
                }
                catch (Exception e) {
                    logger.Error("UNCAUGHT EXCEPTION IN CLIENT TASK for Client [{0}] ! Ending the task. Trace below:".Format(clientId));
                    logger.Error(e.ToString());
                    break;
                }
            }

            lock (clients) {
                clients.RemoveAll(o=>o.id == clientId);
            }

            client.Dispose();
            logger.Info($"Terminating client thread for client (preiouvsly at) id {clientId}");
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

            logger.Debug($"Preparing to send a lobby list to client [{clientId}] for game {query.game}");

            var results = lobbies.FindAll(
                o =>
                {
                    if (
                        query.game == o.game &&
                        (query.title.Length == 0 || o.title.ToLower().Contains(query.title.ToLower())) &&
                        (!query.freeSpotsOnly || o.maxPlayers < o.players) &&
                        (!query.officialOnly || o.isOfficial == true) &&
                        (!query.publicOnly || o.isPrivate == false) &&
                        (!query.strictVersion || o.gameVersion == query.gameVersion)
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

            logger.Info($"Sent a lobby list for game {query.game} ({results.Count} valid lobbies out of {lobbies.Count}) to client [{clientId}]! ({listBuf.Length} bytes)");
        }

        void HandleSubmit(byte[] deserializable, TcpClient client, uint clientId)
        {
            Lobby lobby;
            lobby = Lobby.Deserialize(deserializable);

            logger.Debug($"Preparing to decode a lobby submission from client [{clientId}] for game {lobby.game}");

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
                logger.Debug("Updating existing lobby (ID: " + uIntId + ") (IPV4: " + string.Join(".", lobby.address) + ")");
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
