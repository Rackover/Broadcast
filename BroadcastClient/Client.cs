using Broadcast.Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace Broadcast.Client
{
    public class Client
    {
        const ushort SECONDS_BEFORE_EMPTY_READ = 1;

        public event Action<(byte[] address, ushort port)> onPunchRequest; 

        string game;
        IPAddress address;
        List<Lobby> lobbies = new List<Lobby>();
        TcpClient client;
        Logger logger;

        List<Action> executionQueue = new List<Action>();

        public Client(string masterAddress, string gameName, bool allowOnlyInterNetworkAddress=false)
        {
            logger = new Logger(programName: "B_Client", outputToFile: true);
            game = gameName;

            logger.Debug("Resolving " + masterAddress + "...");
            var addresses = Dns.GetHostAddresses(masterAddress);

            logger.Debug("Filtering InterNetwork addresses...");
            var interAddr = addresses.First(o => !allowOnlyInterNetworkAddress || o.AddressFamily == AddressFamily.InterNetwork);

            if (interAddr == null) {
                var msg = "Could not resolve the master address [" + masterAddress + "] to any address. Broadcast client could not initialize";
                logger.Error(msg);
                throw new WebException(msg);
            }

            logger.Debug("Resolved " + masterAddress + " to " + interAddr.ToString());

            address = interAddr;


            // Main execution queue for async tasks
            Task.Run(async ()=>
            {
                while (true)
                {
                    var tasks = executionQueue.ToArray();
                    foreach (var task in tasks)
                    {
                        task.Invoke();
                        executionQueue.Remove(task);
                    }
                    if (tasks.Length != 0) logger.Trace("Executed " + tasks.Length + " tasks this loop");
                    await Task.Delay(100);
                }
            });

            Start();
        }

        void Enqueue(Action action)
        {
            lock (executionQueue) {
                executionQueue.Add(action);
            }
        }

        void Start()
        {
            if (client != null)
            {
                logger.Debug("Disposing previous client");
                try
                {
                    client.GetStream().Dispose();
                }
                catch (InvalidOperationException)
                {
                    // Nothing to do
                }
                client.Close();
            }
            logger.Debug("Instantiating a new client");
            client = new TcpClient(address.ToString(), Networking.PORT);
            logger.Debug("Done! Setting receive timeout...");
            client.ReceiveTimeout = SECONDS_BEFORE_EMPTY_READ * 1000;

            logger.Debug("Set client ReceiveTimeout to " + (SECONDS_BEFORE_EMPTY_READ * 1000) + "ms");
            logger.Info("Client connected to " + address + ":" + Networking.PORT);
        }

        public IReadOnlyList<Lobby> GetLobbyList()
        {
            return lobbies.AsReadOnly();
        }

        // Async
        public void UpdateLobbyList(Query query = null)
        {
            if (!ConnectIfNotConnected()) return;
            lobbies.Clear();
            NetworkStream stream = client.GetStream();

            // Send the message to the connected TcpServer. 
            if (query == null)
            {
                query = new Query(game);
            }
            List<byte> message = new List<byte>();
            message.Add(Networking.PROTOCOL_QUERY);
            message.AddRange(query.Serialize());
            stream.WriteData(message.ToArray());

            try
            {
                var data = stream.Read();
                lobbies = Lobby.DeserializeList(data);
                logger.Info("Currently {0} lobbies", lobbies.Count);
            }
            catch (Exception e)
            { // Gonna be IOException or SocketException
                logger.Error("Could not update the lobby list:");
                logger.Error(e.ToString());
            }
        }

        // Sync
        public Lobby CreateLobby(string title, byte[] address, uint maxPlayers=8, string gameVersion="???", string map="Unknown")
        {
            var lobby = new Lobby() {
                title = title,
                address = address,
                maxPlayers = maxPlayers,
                gameVersion = gameVersion,
                map = map
            };
            lobby.id = CreateLobby(lobby);
            return lobby;
        }

        // Async
        public void UpdateLobby(Lobby lobby)
        {
            Enqueue(() =>
            {
                logger.Debug("Updating lobby " + lobby.id);
                CreateLobby(lobby); // Same thing
            });
        }

        public uint CreateLobby(Lobby lobby)
        {
            logger.Debug("Creating lobby");
            return SubmitLobby(lobby);
        }

        uint SubmitLobby(Lobby lobby)
        {
            if (!ConnectIfNotConnected()) return 0;
            NetworkStream stream = client.GetStream();

            lobby.game = game;

            // Send the message to the connected TcpServer. 
            List<byte> message = new List<byte>();
            message.Add(Networking.PROTOCOL_SUBMIT);
            message.AddRange(lobby.Serialize());

            try {
                stream.WriteData(message.ToArray());

                var data = stream.Read();

                using (MemoryStream ms = new MemoryStream(data)) {
                    uint myId = BitConverter.ToUInt32(ms.ToArray(), 0);
                    logger.Debug("My lobby ID is {0}", myId);
                    return myId;
                }
            }
            catch(IOException e) {
                logger.Error("Could not submit the lobby:");
                logger.Error(e.ToString());
            }
            catch (SocketException e) {
                logger.Error("Could not submit the lobby:");
                logger.Error(e.ToString());
            }
            catch (ArgumentOutOfRangeException e) {
                logger.Error("No ID was returned upon submitting the lobby, consider the operation invalid:");
                logger.Error(e.ToString());
            }
            return 0; // Means this has had an exception of any kind
        }

        // Async
        public void DestroyLobby(uint lobbyId)
        {
            Enqueue(() =>
            {
                if (!ConnectIfNotConnected()) return;

                NetworkStream stream = client.GetStream();

                try
                {
                    List<byte> message = new List<byte>();
                    message.Add(Networking.PROTOCOL_DELETE);
                    message.AddRange(BitConverter.GetBytes(lobbyId));

                    logger.Debug("Destroying lobby {4}: {0} {1} {2} {3}", message[0], message[1], message[2], message[3], lobbyId);

                    stream.WriteData(message.ToArray());
                }
                catch (IOException e)
                {
                    logger.Error("Could not destroy the lobby:");
                    logger.Error(e.ToString());
                }
                catch (SocketException e)
                {
                    logger.Error("Could not destroy the lobby:");
                    logger.Error(e.ToString());
                }
            });
        }

        // Async
        public void PunchLobby(uint lobbyId)
        {
            logger.Trace("Enqueuing lobby punch instruction for lobby " + lobbyId);
            Enqueue(() =>
            {
                if (!ConnectIfNotConnected()) return;

                NetworkStream stream = client.GetStream();

                try
                {
                    List<byte> message = new List<byte>();
                    message.Add(Networking.PROTOCOL_PUNCH);
                    message.AddRange(BitConverter.GetBytes(lobbyId));

                    logger.Debug("Punching lobby {0}", lobbyId);

                    stream.WriteData(message.ToArray());
                }
                catch (IOException e)
                {
                    logger.Error("Could not punch the lobby:");
                    logger.Error(e.ToString());
                }
                catch (SocketException e)
                {
                    logger.Error("Could not punch the lobby:");
                    logger.Error(e.ToString());
                }
            });
        }

        bool ConnectIfNotConnected()
        {
            if (IsConnected()) {
                logger.Debug("Client already connected, nothing to do.");
                return true;
            }
            else {
                logger.Info("Client not connected, reconnecting...");
                Start();
                if (IsConnected()) {
                    return true;
                }
                else {
                    return false;
                }
            }
        }

        bool IsConnected()
        {
            if (client != null && client.Connected) {
                try {

                    NetworkStream stream = client.GetStream();
                    List<byte> message = new List<byte>();
                    message.Add(Networking.PROTOCOL_HELLO);
                    message.AddRange(Encoding.UTF8.GetBytes("Oh hey!"));
                    stream.WriteData(message.ToArray());

                    var data = stream.Read();
                    
                    if (data.Length > 0) {

                        switch (data[0])
                        {
                            // PROTOCOL--IP1--IP2--IP3--IP4--PORT--PORT
                            case Networking.PROTOCOL_PUNCH:
                                if (data.Length < 7)
                                {
                                    logger.Warn("Ignored punch request from TCP server because of a malformed request (read " + data.Length + " bytes)");
                                    return true;
                                }
                                var address = data[1] + "." + data[2] + "." + data[3] + "." + data[4];
                                var port = BitConverter.ToUInt16(new byte[] { data[5], data[6] }, 0);
                                logger.Debug("Preparing to punch " + address + ":" + port + "...");
                                onPunchRequest.Invoke((new byte[] { data[1], data[2], data[3], data[4] }, port));
                                break;

                            case Networking.PROTOCOL_HELLO:
                                logger.Debug("Received hello from server (" + data.Length + " bytes)");
                                break;

                            default:
                                logger.Warn("Received garbage TCP data from broadcast server (read " + data.Length + " bytes, controller " + data[0] + ")");
                                break;
                        }

                        return true;
                    }
                    else {
                        return false;
                    }
                }
                catch (IOException) {
                    return false;
                }
                catch (SocketException) {
                    return false;
                }
            }
            else {
                return false;
            }
        }

        /// <summary>
        /// Test function
        /// </summary>
        public void Test()
        {
            List<uint> createdLobbies = new List<uint>();
            logger.Trace("TEST --> Starting test");
            while (true) {
                try {
                    logger.Trace("TEST --> Updating lobby list");
                    UpdateLobbyList(new Query("test"));
                    Thread.Sleep(1000);
                    if (new Random().Next(0, 3) < 2) {
                        logger.Trace("TEST --> Creating new lobby");
                        var myLobby = CreateLobby(new Lobby() { game = "test" });
                        createdLobbies.Add(myLobby);
                        logger.Trace("TEST --> Successfully created lobby " + myLobby + "");
                        Thread.Sleep(5000);
                    }
                    else if (new Random().Next(0, 3) < 1 && createdLobbies.Count > 0) {
                        logger.Trace("TEST --> Destroying a created lobby");
                        DestroyLobby(createdLobbies[0]);
                        createdLobbies.RemoveAt(0);
                        Thread.Sleep(5000);
                    }
                    else if (createdLobbies.Count > 0)
                    {
                        logger.Trace("TEST --> Punching a created lobby ("+ createdLobbies[0]+")");
                        PunchLobby(createdLobbies[0]);
                        Thread.Sleep(2000);
                    }
                    logger.Trace("TEST --> End of decision loop");
                }
                catch (IOException) {
                    Thread.Sleep(3000);
                    logger.Trace("TEST --> Server out, retrying...");
                    client.Close();
                }
                catch (SocketException) {
                    Thread.Sleep(3000);
                    logger.Trace("TEST --> Server dead, retrying...");
                    client.Close();
                }
                catch (Exception e) {
                    Console.ForegroundColor = ConsoleColor.Red;
                    logger.Info("TEST --> THIS EXCEPTION SHOULD NOT HAPPEN!");
                    Console.ForegroundColor = ConsoleColor.Gray;
                    logger.Trace(e.ToString());
                    client.Close();
                }
            }
        }


    }
}
