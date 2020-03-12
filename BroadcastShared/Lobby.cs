using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace Broadcast.Shared
{
    [Serializable]
    public class Lobby
    {
        public readonly byte broadcastVersion = Networking.VERSION;
        public string game = string.Empty;
        public uint id;

        public string secretKey = string.Empty;
        public string gameVersion = string.Empty;
        public uint players = 0;
        public uint maxPlayers = 0;
        public bool requireAuth = false;
        public bool isOfficial = false;
        public string map = string.Empty;
        public string[] mods = new string[]{};
        public string title = string.Empty;
        public string description = string.Empty;
        public bool isPrivate = false;

        public byte[] address = new byte[4];
        public string strAddress = string.Empty;
        public ushort port = 1;
        public EProtocol protocol = EProtocol.IPv4;


        public byte[] Serialize(){
            byte[] span;
            using (MemoryStream ms = new MemoryStream()){
                using (BinaryWriter bw = new BinaryWriter(ms)){
                    bw.Write(broadcastVersion);
                    bw.Write(game);
                    bw.Write(id);
                    bw.Write(secretKey);
                    bw.Write(gameVersion);
                    bw.Write(players);
                    bw.Write(maxPlayers);
                    bw.Write(requireAuth);
                    bw.Write(isOfficial);
                    bw.Write(map);
                    bw.Write(string.Join("|", mods));
                    bw.Write(title);
                    bw.Write(description);
                    bw.Write(isPrivate);
                    bw.Write(address);
                    bw.Write(strAddress);
                    bw.Write(port);
                    bw.Write((byte)protocol);
                }
                span = ms.ToArray();
            }
            return span;
        }

        public static Lobby Deserialize(byte[] data){
            Lobby lobby = new Lobby();
            using (MemoryStream ms = new MemoryStream(data)){
                using (BinaryReader br = new BinaryReader(ms)){
                    FillLobby(lobby, br);
                }
            }
            return lobby;
        }

        static void FillLobby(Lobby lobby, BinaryReader br){
            br.ReadByte(); // Skipping BroadcastVersion
            lobby.game = br.ReadString();
            lobby.id = br.ReadUInt32();
            lobby.secretKey = br.ReadString();
            lobby.gameVersion = br.ReadString();
            lobby.players = br.ReadUInt32();
            lobby.maxPlayers = br.ReadUInt32();
            lobby.requireAuth = br.ReadBoolean();
            lobby.isOfficial = br.ReadBoolean();
            lobby.map = br.ReadString();
            lobby.mods = br.ReadString().Split('|');
            lobby.title = br.ReadString();
            lobby.description = br.ReadString();
            lobby.isPrivate = br.ReadBoolean();
            lobby.address = br.ReadBytes(4);
            lobby.strAddress = br.ReadString();
            lobby.port = br.ReadUInt16();
            lobby.protocol = (EProtocol)br.ReadByte();
        }

        public static byte[] SerializeList(List<Lobby> lobbies){
            byte[] span;
            using (MemoryStream ms = new MemoryStream()){
                using (BinaryWriter bw = new BinaryWriter(ms)){
                    bw.Write((UInt16)(lobbies.Count));
                    foreach(var lobby in lobbies){
                        bw.Write(lobby.Serialize());
                    }
                    span = ms.ToArray();
                }
            }
            return span;
        }

        public static List<Lobby> DeserializeList(byte[] data){
            var lobbies = new List<Lobby>();
            using (MemoryStream ms = new MemoryStream(data)){
                using (BinaryReader br = new BinaryReader(ms)){
                    var listSize = br.ReadUInt16();
                    for (var _ = 0; _ < listSize; _++){
                        var lob = new Lobby();
                        FillLobby(lob, br);
                        lobbies.Add(lob);
                    }
                }
            }
            return lobbies;
        }

        public bool HasAddress()
        {
            return (address != null && !address.IsSameAs(new byte[4])) || !string.IsNullOrEmpty(strAddress);
        }
    }
}
