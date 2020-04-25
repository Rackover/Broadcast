using Broadcast.Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Broadcast.Client
{
    public class Client
    {
        const ushort SECONDS_BEFORE_EMPTY_READ = 1;

        string game;
        string address;
        List<Lobby> lobbies = new List<Lobby>();
        TcpClient client;

        public Client(string masterAddress, string gameName)
        {
            game = gameName;
            address = masterAddress;
        }

        void Start()
        {
            if (client!= null) {
                client.Dispose();
            }
            client = new TcpClient(address, Networking.PORT);
            client.GetStream().ReadTimeout = SECONDS_BEFORE_EMPTY_READ * 1000;
            Console.WriteLine("Client connected to "+address+":"+Networking.PORT);
        }

        public IReadOnlyList<Lobby> GetLobbyList()
        {
            return lobbies.AsReadOnly();
        }

        public void UpdateLobbyList(Query query = null)
        {
            if (!ConnectIfNotConnected()) return;
            NetworkStream stream = client.GetStream();

            // Send the message to the connected TcpServer. 
            if (query == null) {
                query = new Query() {
                    game = game
                };
            }
            List<byte> message = new List<byte>();
            message.Add(Networking.PROTOCOL_QUERY);
            message.AddRange(query.Serialize());
            stream.WriteData(message.ToArray());

            var data = stream.Read();
            lobbies = Lobby.DeserializeList(data);
            Console.WriteLine("Currently {0} lobbies", lobbies.Count);
            
        }

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

        public void UpdateLobby(Lobby lobby)
        {
            Console.WriteLine("Updating lobby " + lobby.id);
            CreateLobby(lobby); // Same thing
        }

        public uint CreateLobby(Lobby lobby)
        {
            Console.WriteLine("Creating lobby");
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

            stream.WriteData(message.ToArray());

            var data = stream.Read();

            using (MemoryStream ms = new MemoryStream(data)) {
                uint myId = BitConverter.ToUInt32(ms.ToArray(), 0);
                Console.WriteLine("My lobby ID is {0}", myId);
                return myId;
            }
        }

        public void DestroyLobby( uint lobbyId)
        {
            if (!ConnectIfNotConnected()) return; 

            NetworkStream stream = client.GetStream();

            List<byte> message = new List<byte>();
            using (MemoryStream ms = new MemoryStream()) {
                message.Add(Networking.PROTOCOL_DELETE);
                message.AddRange(BitConverter.GetBytes(lobbyId));

                Console.WriteLine("Destroying lobby {4}: {0} {1} {2} {3}", message[0], message[1], message[2], message[3], lobbyId);

                stream.WriteData(message.ToArray());
            }

        }

        bool ConnectIfNotConnected()
        {
            if (IsConnected()) {
                Console.WriteLine("Client already connected, nothing to do.");
                return true;
            }
            else {
                Console.WriteLine("Client not connected, reconnecting...");
                Start();
                if (IsConnected()) {
                    return true;
                }
                else {
                    return false;
                }
            }
        }

        public bool IsConnected()
        {
            return client != null && client.Connected;
        }

        /// <summary>
        /// Test function
        /// </summary>
        public void Test()
        {
            List<uint> createdLobbies = new List<uint>();
            Console.WriteLine("TEST --> Starting test");
            while (true) {
                try {
                    Console.WriteLine("TEST --> Updating lobby list");
                    UpdateLobbyList(new Query() { game = "test" });
                    Thread.Sleep(1000);
                    if (new Random().Next(0, 3) < 2) {
                        Console.WriteLine("TEST --> Creating new lobby");
                        var myLobby = CreateLobby(new Lobby() { game = "test" });
                        createdLobbies.Add(myLobby);
                        Thread.Sleep(5000);
                    }
                    else if (new Random().Next(0, 3) < 2 && createdLobbies.Count > 0) {
                        Console.WriteLine("TEST --> Destroying a created lobby");
                        DestroyLobby(createdLobbies[0]);
                        createdLobbies.RemoveAt(0);
                        Thread.Sleep(5000);
                    }
                    Console.WriteLine("TEST --> End of decision loop");
                }
                catch (IOException) {
                    Thread.Sleep(3000);
                    Console.WriteLine("TEST --> Server out, retrying...");
                    client.Close();
                }
                catch (SocketException) {
                    Thread.Sleep(3000);
                    Console.WriteLine("TEST --> Server dead, retrying...");
                    client.Close();
                }
                catch (Exception e) {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("TEST --> THIS EXCEPTION SHOULD NOT HAPPEN!");
                    Console.ForegroundColor = ConsoleColor.Gray;
                    Console.WriteLine(e);
                    client.Close();
                }
            }
        }


    }
}
