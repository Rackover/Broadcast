using MessagePack;
using System;
using System.Collections.Generic;
using System.Text;

namespace Broadcast.Shared
{
    [Serializable]
    [MessagePackObject]
    public class Query
    {
        [Key(0)] public byte broadcastVersion = 1;
        [Key(1)] public string game;
        [Key(2)] public string gameVersion = "";

        [Key(3)] public bool officialOnly = false;
        [Key(4)] public bool freeSpotsOnly = false;
        [Key(5)] public bool publicOnly = false;
        [Key(6)] public bool strictVersion = false;
    }
}
