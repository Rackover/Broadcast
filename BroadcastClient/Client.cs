using Broadcast.Shared;
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
        static string game = "test";
        static List<Lobby> lobbies = new List<Lobby>();
        static TcpClient client;

        public static void Start(string addr)
        {
            client = new TcpClient(addr, Networking.PORT);
        }

        public static void Test()
        {
            NetworkStream stream = client.GetStream();

            try {
                while (true) {
                    CheckForLobbies(stream);
                    Thread.Sleep(1000);
                    if (new Random().Next(0, 3) < 2) {
                        CreateLobby(stream);
                        Thread.Sleep(1000);
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

        static void CheckForLobbies(NetworkStream stream)
        {
            // Send the message to the connected TcpServer. 
            var query = new Query() {
                game = game
            };
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

        static void CreateLobby(NetworkStream stream)
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

                Console.WriteLine("Sending NEW Lobby {0} {1} {2} {3}", message[0], message[1], message[2], message[3]);

                stream.WriteData(message.ToArray());
            }


            var data = stream.Read();

            using (MemoryStream ms = new MemoryStream(data)) {
                uint myId = BitConverter.ToUInt32(ms.ToArray());
                Console.WriteLine("My lobby ID is {0}", myId);
            }
        }
    }
}
