using System;
using System.Collections.Generic;
using System.Text;

namespace Broadcast.Shared
{
    [Serializable]
    public class Query
    {
        public byte broadcastVersion = 1;
        public string game;
        public string gameVersion = "";

        public bool officialOnly = false;
        public bool freeSpotsOnly = false;
        public bool publicOnly = false;
        public bool strictVersion = false;
    }
}
