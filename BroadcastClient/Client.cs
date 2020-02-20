﻿using Broadcast.Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
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

        public static void Start(string addr, string gameName = "MyGame")
        {
            game = gameName;
            client = new TcpClient(addr, Networking.PORT);
        }

        static public void CheckForLobbies(NetworkStream stream, Query query = null)
        {
            // Send the message to the connected TcpServer. 
            if (query == null) {
                query = new Query() {
                    game = game
                };
            }
            var bf = new BinaryFormatter();

            List<byte> message = new List<byte>();
            using (MemoryStream ms = new MemoryStream()) {
                bf.Serialize(ms, query);
                message.Add(Networking.PROTOCOL_QUERY);
                message.AddRange(ms.ToArray());

                Console.WriteLine("Sending query {0} {1} {2} {3}", message[0], message[1], message[2], message[3]);

                stream.WriteData(message.ToArray());
            }


            var data = stream.Read();
            using (MemoryStream ms = new MemoryStream(data)) {
                Console.WriteLine("Reading " + data.Length);
                lobbies = (List<Lobby>)bf.Deserialize(ms);
                Console.WriteLine("Currently {0} lobbies", lobbies.Count);
            }
        }

        static public uint CreateLobby(NetworkStream stream, Lobby lobby)
        {
            // Send the message to the connected TcpServer. 
            var query = new Lobby() { 
                game = game,
                map = "de_dust2"
            };
            var bf = new BinaryFormatter();

            List<byte> message = new List<byte>();
            using (MemoryStream ms = new MemoryStream()) {
                bf.Serialize(ms, query);
                message.Add(Networking.PROTOCOL_SUBMIT);
                message.AddRange(ms.ToArray());

                Console.WriteLine("Creating NEW Lobby {0} {1} {2} {3}", message[0], message[1], message[2], message[3]);

                stream.WriteData(message.ToArray());
            }


            var data = stream.Read();

            using (MemoryStream ms = new MemoryStream(data)) {
                uint myId = BitConverter.ToUInt32(ms.ToArray());
                Console.WriteLine("My lobby ID is {0}", myId);
                return myId;
            }
        }

        static public void DestroyLobby(NetworkStream stream, uint lobbyId)
        {
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
            NetworkStream stream = client.GetStream();
            List<uint> createdLobbies = new List<uint>();
            try {
                while (true) {
                    CheckForLobbies(stream, new Query() { game = "test" });
                    Thread.Sleep(1000);
                    if (new Random().Next(0, 3) < 2) {
                        createdLobbies.Add(CreateLobby(stream, new Lobby() { game = "test" }));
                        Thread.Sleep(5000);
                    }
                    else if (new Random().Next(0, 3) < 2 && createdLobbies.Count > 0) {
                        DestroyLobby(stream, createdLobbies[0]);
                        createdLobbies.RemoveAt(0);
                        Thread.Sleep(5000);
                    }
                }
            }
            catch (IOException) {
                Thread.Sleep(3000);
                Console.WriteLine("Server out, retrying...");
                stream.Close();
                client.Close();
            }
            catch (SocketException) {
                Thread.Sleep(3000);
                Console.WriteLine("Server dead, retrying...");
                stream.Close();
                client.Close();
            }
        }


    }
}
