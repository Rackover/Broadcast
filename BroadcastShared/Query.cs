using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace Broadcast.Shared
{
    [Serializable]
    public class Query
    {
        public readonly byte broadcastVersion = Networking.VERSION;
        public string game;
        public string gameVersion = "";

        public bool officialOnly = false;
        public bool freeSpotsOnly = false;
        public bool publicOnly = false;
        public bool strictVersion = false;

         public byte[] Serialize(){
            byte[] span;
            using (MemoryStream ms = new MemoryStream()){
                using (BinaryWriter bw = new BinaryWriter(ms)){
                    bw.Write(broadcastVersion);
                    bw.Write(game);
                    bw.Write(gameVersion);
                    bw.Write(officialOnly);
                    bw.Write(freeSpotsOnly);
                    bw.Write(publicOnly);
                    bw.Write(strictVersion);
                }
                span = ms.ToArray();
            }
            return span;
        }

        public static Query Deserialize(byte[] data){
            Query lobby = new Query();
            using (MemoryStream ms = new MemoryStream(data)){
                using (BinaryReader br = new BinaryReader(ms)){
                    br.ReadByte();
                    lobby.game = br.ReadString();
                    lobby.gameVersion = br.ReadString();
                    lobby.officialOnly = br.ReadBoolean();
                    lobby.freeSpotsOnly = br.ReadBoolean();
                    lobby.publicOnly = br.ReadBoolean();
                    lobby.strictVersion = br.ReadBoolean();
                }
            }
            return lobby;
        }   
    }

    
}
