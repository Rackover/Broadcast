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
        public string title = "";

        Query(){

        }

        public Query(string game){
            this.game = game;
        }

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
                    bw.Write(title);
                }
                span = ms.ToArray();
            }
            return span;
        }

        public static Query Deserialize(byte[] data){
            Query query = new Query();
            using (MemoryStream ms = new MemoryStream(data)){
                using (BinaryReader br = new BinaryReader(ms)){
                    br.ReadByte();
                    query.game = br.ReadString();
                    query.gameVersion = br.ReadString();
                    query.officialOnly = br.ReadBoolean();
                    query.freeSpotsOnly = br.ReadBoolean();
                    query.publicOnly = br.ReadBoolean();
                    query.strictVersion = br.ReadBoolean();
                    query.title = br.ReadString();
                }
            }
            return query;
        }   
    }

    
}
