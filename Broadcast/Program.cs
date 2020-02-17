using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Net;      //required
using System.Net.Sockets;    //required
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

using Broadcast.Shared;

namespace Broadcast.Server
{
    class Program
    {
        const ushort RESPONSE_SIZE = 200;
        const byte VERSION = 1;

        static void Main(string[] args)
        {
            TcpListener server = new TcpListener(IPAddress.Any, Networking.PORT);
            // we set our IP address as server's address, and we also set the port: 9999

            var bf = new BinaryFormatter();
            var lobbies = new List<Lobby>();


            server.Start();  // this will start the server
            Console.WriteLine("Ready - Port " + Networking.PORT + " - Version " + VERSION);
            while (true)   //we wait for a connection
            {
                TcpClient client = server.AcceptTcpClient();  //if a connection exists, the server will accept it
                uint clientId = (uint)new Random().Next(int.MaxValue);
                Console.WriteLine(">ENTER "+ clientId);

                new Task(delegate {
                    NetworkStream ns = client.GetStream(); 
                    while (client.Connected)  //while the client is connected, we look for incoming messages
                    {
                        try {
                            var lengthBuffer = new byte[4]; // uint is 4 bytes

                            // BLOCKing
                            var msgBuffer = ns.Read();
                            Console.WriteLine("Received message {0} {1} {2} {3}", msgBuffer[0], msgBuffer[1], msgBuffer[2], msgBuffer[3]);


                            var messageType = msgBuffer[0];
                            byte[] deserializable = new byte[msgBuffer.Length - 1];
                            Array.Copy(msgBuffer, 1, deserializable, 0, deserializable.Length);

                            switch (messageType) {
                                case Networking.PROTOCOL_QUERY: // QUERY
                                    Query query;
                                    using (MemoryStream ms = new MemoryStream(deserializable)) {
                                        query = (Query)bf.Deserialize(ms);
                                    }
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

                                    using (MemoryStream ms = new MemoryStream()) {
                                        bf.Serialize(ms, results);
                                        ns.WriteData(ms.ToArray());
                                    }

                                    break;

                                case Networking.PROTOCOL_SUBMIT: // SUBMIT
                                    Lobby lobby;
                                    using (MemoryStream ms = new MemoryStream(deserializable)) {
                                        lobby = (Lobby)bf.Deserialize(ms);
                                    }

                                    var index = lobbies.FindIndex(o => o.id == lobby.id);
                                    uint uIntId = 0;
                                    if (index > -1) {
                                        uIntId = lobbies[index].id;
                                        lobbies[index] = lobby;
                                    }
                                    else {
                                        uIntId = Convert.ToUInt32(Math.Floor(new Random().NextDouble() * (uint.MaxValue-1)) + 1);
                                        lobby.id = uIntId;
                                        lobbies.Add(lobby);
                                    }
                                    var id = BitConverter.GetBytes(uIntId);
                                    Array.Reverse(id);
                                    ns.WriteData(id); // I return the ID of the lobby
                                    break;

                                case Networking.PROTOCOL_DELETE: // DELETE

                                    break;

                                default:
                                    // ???
                                    Console.WriteLine("FROM " + clientId + " => ???? Message type is " + messageType);
                                    break;
                            }
                        }
                        catch(IOException) {
                            break;
                        }
                        catch(SocketException) {
                            break;
                        }
                    }
                    Console.WriteLine(">EXIT " + clientId);
                }).Start();
            }
        }


    }
}