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
        const ushort HOURS_BEFORE_CLEANUP = 24;

        List<Lobby> lobbies = new List<Lobby>();
        Dictionary<Lobby, DateTime> lastHeardAbout = new Dictionary<Lobby, DateTime>();

        public Server()
        {
            TcpListener server = new TcpListener(IPAddress.Any, Networking.PORT);
            // we set our IP address as server's address, and we also set the port: 9999
            
            var controller = new Dictionary<byte, Action<byte[], NetworkStream>>() {
                {Networking.PROTOCOL_SUBMIT, HandleSubmit },
                {Networking.PROTOCOL_QUERY, HandleQuery },
                {Networking.PROTOCOL_DELETE, HandleDelete },
            };


            server.Start();  // this will start the server
            Console.WriteLine("Broadcast ready - Port " + Networking.PORT + " - Version " + VERSION);
            while (true)   //we wait for a connection
            {
                TcpClient client = server.AcceptTcpClient();  //if a connection exists, the server will accept it
                uint clientId = (uint)new Random().Next(int.MaxValue);
                Console.WriteLine("> ENTER " + clientId);

                new Task(delegate {
                    NetworkStream ns = client.GetStream();
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
                                Console.WriteLine("> {0} > {1} {2} bytes", clientId, messageType, deserializable.Length);
                                controller[messageType](deserializable, ns);
                            }
                            else { 
                                // ???
                                Console.WriteLine("> " + clientId + " > ???? Message type is " + messageType+", not implemented in this broadcast version");
                            }
                        }
                        catch (IOException) {
                            break;
                        }
                        catch (SocketException) {
                            break;
                        }
                    }
                    Console.WriteLine("> EXIT " + clientId);
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
                if (DateTime.UtcNow.Subtract(lastTime).TotalHours > HOURS_BEFORE_CLEANUP) {
                    lock (lobbies) {
                        lobbies.RemoveAll(o => o.id == lobby.id);
                        removed++;
                    }
                }
            }
            if (removed > 0) Console.WriteLine("Cleanup finished, removed " + removed + " lobbies");
        }

        void HandleQuery(byte[] deserializable, NetworkStream ns)
        {
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


            ns.WriteData(Lobby.SerializeList(lobbies));
        }

        void HandleSubmit(byte[] deserializable, NetworkStream ns)
        {
            Lobby lobby;
            lobby = Lobby.Deserialize(deserializable);

            var secretKey = Environment.GetEnvironmentVariable("BROADCAST_GAME_KEY_"+lobby.game);
            if (secretKey != null && secretKey == lobby.secretKey) {
                // Forbidden
                return;
            }

            var index = lobbies.FindIndex(o => o.id == lobby.id);
            uint uIntId = 0;
            if (index > -1) {
                uIntId = lobbies[index].id;
                lobbies[index] = lobby;
            }
            else {
                uIntId = Convert.ToUInt32(Math.Floor(new Random().NextDouble() * (uint.MaxValue - 1)) + 1);
                lobby.id = uIntId;
                lobbies.Add(lobby);
            }
            lastHeardAbout[lobby] = DateTime.UtcNow;
            var id = BitConverter.GetBytes(uIntId);
            Array.Reverse(id);
            ns.WriteData(id); // I return the ID of the lobby
        }

        void HandleDelete(byte[] deserializable, NetworkStream ns)
        {
            var targetLobby = BitConverter.ToUInt32(deserializable);
            lock (lobbies) {
                lobbies.RemoveAll(o => o.id == targetLobby);
            }
        }
    }
}
