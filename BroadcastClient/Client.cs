﻿namespace Broadcast.Client
{
    using Broadcast.Shared;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading.Tasks;
    using System.Linq;

    public class Client : IDisposable
    {
        public event Action<(byte[] address, ushort port)> OnPunchRequest;

        private readonly string game;
        private readonly string address;
        private readonly Logger logger;
        private Socket client;

        private readonly List<Lobby> cachedLobbies = new List<Lobby>();

        public Client(string masterAddress, string gameName)
        {
            logger = new Logger(programName: "B_Client", outputToFile: true);
            game = gameName;

            logger.Info($"Initializing broadcast client with game name [{game}] and master address {masterAddress}");

            address = masterAddress;
        }

        public IReadOnlyList<Lobby> GetCachedLobbyList()
        {
            return cachedLobbies.AsReadOnly();
        }

        private async Task Start()
        {
            try {
                if (client != null && client.Connected) {
                    logger.Debug($"Disconnecting client socket from previous connection");
                    client.Shutdown(SocketShutdown.Both);
                    client.Disconnect(reuseSocket:false);
                    client.Dispose();
                }

                client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                client.ReceiveTimeout = Broadcast.Shared.Networking.TIMEOUT_MS;
                client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontLinger, true);

                logger.Debug($"Connecting to {address} at port {Networking.PORT}...");

                await client.ConnectAsync(address.ToString(), Networking.PORT);

                logger.Info("Client connected to " + address + ":" + Networking.PORT);
            }
            catch (Exception ex) {
                logger.Error($"Could not start broadcast client! {ex}");
            }
        }


        // Async
        public async Task<List<Lobby>> FetchLobbies(Query query = null)
        {
            bool connectionOK = await ConnectIfNotConnected();
            if (!connectionOK) return null;

            using (NetworkStream stream = new NetworkStream(client, ownsSocket: false)) {
                // Send the message to the connected TcpServer. 
                if (query == null) {
                    query = new Query(game);
                }

                List<byte> message = new List<byte>();
                message.Add(Networking.PROTOCOL_QUERY);
                message.AddRange(query.Serialize());
                stream.WriteData(message.ToArray());

                try {
                    byte[] data = stream.ReadUntilControllerFound(Networking.PROTOCOL_QUERY_RESPONSE);

                    if (data != null) {
                        List<Lobby> lobbies = Lobby.DeserializeList(data);
                        logger.Info($"Fetched {lobbies.Count} lobbies (list was {data.Length} bytes long)");

                        lock (cachedLobbies) {
                            cachedLobbies.Clear();
                            cachedLobbies.AddRange(lobbies);
                        }

                        return lobbies;
                    }
                }
                catch (Exception e) { // Gonna be IOException or SocketException
                    logger.Error("Could not update the lobby list:");
                    logger.Error(e.ToString());
                }
            }

            return null;
        }

        // Sync
        public bool CreateLobby(string title, byte[] address, out Lobby lobby, uint maxPlayers = 8, string gameVersion = "???", string map = "Unknown")
        {
            lobby = new Lobby() {
                title = title,
                address = address,
                maxPlayers = maxPlayers,
                gameVersion = gameVersion,
                map = map
            };

            return CreateLobby(lobby);
        }

        public async Task UpdateLobby(Lobby lobby)
        {
            await Task.Run(() =>
            {
                logger.Debug("Updating lobby " + lobby.id);
                CreateLobby(lobby); // Same thing
            });
        }

        public bool CreateLobby(Lobby lobby)
        {
            logger.Debug("Creating lobby");

            if (SubmitLobby(lobby, out uint id)) {
                lobby.id = id;
                return true;
            }

            return false;
        }

        bool SubmitLobby(Lobby lobby, out uint lobbyID)
        {
            lobbyID = 0;

            if (!Task.Run(async () =>
            {
                await ConnectIfNotConnected();
            }).Wait(Networking.TIMEOUT_MS)) { // Five seconds to connect, worst case
                return false;
            }

            if (!IsConnected()) return false;

            using (NetworkStream stream = new NetworkStream(client, ownsSocket: false)) {

                lobby.game = game;

                // Send the message to the connected TcpServer. 
                List<byte> message = new List<byte>();
                message.Add(Networking.PROTOCOL_SUBMIT);
                message.AddRange(lobby.Serialize());

                try {
                    stream.WriteData(message.ToArray());

                    byte[] data = stream.ReadUntilControllerFound(Networking.PROTOCOL_SUBMISSION_RESPONSE);

                    if (data != null) {
                        using (MemoryStream ms = new MemoryStream(data)) {
                            ms.Seek(1, SeekOrigin.Begin); // Skip controller head
                            uint myId = BitConverter.ToUInt32(ms.ToArray(), 0);
                            logger.Debug("My lobby ID is {0}".Format(myId));
                            lobbyID = myId;
                            return true;
                        }
                    }
                }
                catch (IOException e) {
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
            }

            return false; // Means this has had an exception of any kind
        }

        // Async
        public async Task DestroyLobby(uint lobbyId)
        {
            if (!await ConnectIfNotConnected()) return;


            try {
                List<byte> message = new List<byte>();
                message.Add(Networking.PROTOCOL_DELETE);
                message.AddRange(BitConverter.GetBytes(lobbyId));

                logger.Debug("Destroying lobby {4}: {0} {1} {2} {3}".Format(message[0], message[1], message[2], message[3], lobbyId));
                using (NetworkStream stream = new NetworkStream(client, ownsSocket: false)) {
                    stream.WriteData(message.ToArray());
                }
            }
            catch (IOException e) {
                logger.Error("Could not destroy the lobby:");
                logger.Error(e.ToString());
            }
            catch (SocketException e) {
                logger.Error("Could not destroy the lobby:");
                logger.Error(e.ToString());
            }
        }

        public async Task PunchLobby(uint lobbyId)
        {
            logger.Trace("Enqueuing lobby punch instruction for lobby " + lobbyId);
            if (!await ConnectIfNotConnected()) return;

            try {
                List<byte> message = new List<byte>();
                message.Add(Networking.PROTOCOL_REQUEST_PUNCH_TO_LOBBY);
                message.AddRange(BitConverter.GetBytes(lobbyId));

                logger.Debug("Punching lobby {0}".Format(lobbyId));
                using (NetworkStream stream = new NetworkStream(client, ownsSocket: false)) {

                    stream.WriteData(message.ToArray());
                }
            }
            catch (IOException e) {
                logger.Error("Could not punch the lobby:");
                logger.Error(e.ToString());
            }
            catch (SocketException e) {
                logger.Error("Could not punch the lobby:");
                logger.Error(e.ToString());
            }
        }

        async Task<bool> ConnectIfNotConnected()
        {
            if (IsConnected()) {
                logger.Debug("Client already connected, nothing to do.");
                return true;
            }
            else {
                logger.Info("Client not connected, reconnecting...");

                try {
                    await Start();
                }
                catch (Exception e) {
                    logger.Info($"Could not connect! {e}");
                    return false;
                }

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
                    using (NetworkStream stream = new NetworkStream(client, ownsSocket: false)) {

                        List<byte> message = new List<byte>();
                        message.Add(Networking.PROTOCOL_HELLO);
                        message.AddRange(Encoding.UTF8.GetBytes("Oh hey!"));
                        stream.WriteData(message.ToArray());

                        byte[] data = stream.ReadUntilControllerFound(out byte controllerFound, Networking.PROTOCOL_HELLO_PLEASE_PUNCH, Networking.PROTOCOL_HELLO);

                        if (data.Length > 0) {

                            switch (controllerFound) {
                                // PROTOCOL--IP1--IP2--IP3--IP4--PORT--PORT
                                case Networking.PROTOCOL_HELLO_PLEASE_PUNCH:
                                    if (data.Length < 6) {
                                        logger.Warn("Ignored punch request from TCP server because of a malformed request (read " + data.Length + " bytes)");
                                        return true;
                                    }

                                    var address = data[0] + "." + data[1] + "." + data[2] + "." + data[3];
                                    var port = BitConverter.ToUInt16(new byte[] { data[4], data[5] }, 0);
                                    logger.Debug("Invoking punch at " + address + ":" + port + "...");
                                    OnPunchRequest?.Invoke((new byte[] { data[0], data[1], data[2], data[3] }, port));
                                    break;

                                case Networking.PROTOCOL_HELLO:
                                    logger.Debug("Received hello from server (" + data.Length + " bytes)");
                                    break;
                            }

                            return true;
                        }
                        else {
                            return false;
                        }
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
        public async Task Test()
        {
            List<uint> createdLobbies = new List<uint>();
            logger.Trace("TEST --> Starting test");
            while (true) {
                try {
                    logger.Trace("TEST --> Updating lobby list");
                    await FetchLobbies(new Query("test"));
                    await Task.Delay(1000);
                    if (new Random().Next(0, 3) < 2) {
                        logger.Trace("TEST --> Creating new lobby");

                        var lobby = new Lobby() { game = "test" };
                        if (CreateLobby(lobby)) {
                            createdLobbies.Add(lobby.id);

                            logger.Trace("TEST --> Successfully created lobby " + lobby.id + "");
                        }
                        else {
                            logger.Error("TEST --> Failed to create lobby!");
                        }

                        await Task.Delay(5000);
                    }
                    else if (new Random().Next(0, 3) < 1 && createdLobbies.Count > 0) {
                        logger.Trace("TEST --> Destroying a created lobby");
                        await DestroyLobby(createdLobbies[0]);
                        createdLobbies.RemoveAt(0);
                        await Task.Delay(5000);
                    }
                    else if (createdLobbies.Count > 0) {
                        logger.Trace("TEST --> Punching a created lobby (" + createdLobbies[0] + ")");
                        await PunchLobby(createdLobbies[0]);
                        await Task.Delay(2000);
                    }

                    logger.Trace("TEST --> End of decision loop");
                }
                catch (IOException) {
                    await Task.Delay(3000);
                    logger.Trace("TEST --> Server out, retrying...");
                    client.Close();
                }
                catch (SocketException) {
                    await Task.Delay(3000);
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

        public void Dispose()
        {
            ((IDisposable)client).Dispose();
        }
    }
}
