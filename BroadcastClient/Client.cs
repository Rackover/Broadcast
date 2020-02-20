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
        static string game;
        static List<Lobby> lobbies = new List<Lobby>();
        static TcpClient client;

        public static void Start(string masterAddress, string gameName)
        {
            game = gameName;
            client = new TcpClient(masterAddress, Networking.PORT);
        }

        static public IReadOnlyList<Lobby> GetLobbyList()
        {
            return lobbies.AsReadOnly();
        }

        static public void UpdateLobbyList(Query query = null)
        {
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

        static public Lobby CreateLobby(string title, byte[] address, uint currentPlayers=0, uint maxPlayers=8, string gameVersion="???", string map="Unknown")
        {
            var lobby = new Lobby() {
                title = title,
                address = address,
                players = currentPlayers,
                maxPlayers = maxPlayers,
                gameVersion = gameVersion,
                map = map
            };
            lobby.id = CreateLobby(lobby);
            return lobby;
        }

        static public uint CreateLobby(Lobby lobby)
        {
            NetworkStream stream = client.GetStream();

            lobby.game = game;

            // Send the message to the connected TcpServer. 
            var query = lobby;

            List<byte> message = new List<byte>();
            message.Add(Networking.PROTOCOL_SUBMIT);
            message.AddRange(lobby.Serialize());

            Console.WriteLine("Creating NEW Lobby {0} {1} {2} {3}", message[0], message[1], message[2], message[3]);

            stream.WriteData(message.ToArray());

            var data = stream.Read();

            using (MemoryStream ms = new MemoryStream(data)) {
                uint myId = BitConverter.ToUInt32(ms.ToArray());
                Console.WriteLine("My lobby ID is {0}", myId);
                return myId;
            }
        }

        static public void DestroyLobby( uint lobbyId)
        {
            NetworkStream stream = client.GetStream();

            List<byte> message = new List<byte>();
            using (MemoryStream ms = new MemoryStream()) {
                message.Add(Networking.PROTOCOL_DELETE);
                message.AddRange(BitConverter.GetBytes(lobbyId));

                Console.WriteLine("Destroying lobby {4}: {0} {1} {2} {3}", message[0], message[1], message[2], message[3], lobbyId);

                stream.WriteData(message.ToArray());
            }

        }

        /// <summary>
        /// Test function
        /// </summary>
        public static void Test()
        {
            List<uint> createdLobbies = new List<uint>();
            Console.WriteLine("Starting test");
            try {
                while (true) {
                    UpdateLobbyList(new Query() { game = "test" });
                    Thread.Sleep(1000);
                    if (new Random().Next(0, 3) < 2) {
                        createdLobbies.Add(CreateLobby(new Lobby() { game = "test" }));
                        Thread.Sleep(5000);
                    }
                    else if (new Random().Next(0, 3) < 2 && createdLobbies.Count > 0) {
                        DestroyLobby(createdLobbies[0]);
                        createdLobbies.RemoveAt(0);
                        Thread.Sleep(5000);
                    }
                }
            }
            catch (IOException) {
                Thread.Sleep(3000);
                Console.WriteLine("Server out, retrying...");
                client.Close();
            }
            catch (SocketException) {
                Thread.Sleep(3000);
                Console.WriteLine("Server dead, retrying...");
                client.Close();
            }
        }


    }
}
