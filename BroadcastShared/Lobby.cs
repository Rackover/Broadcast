using System;
using System.Collections.Generic;
using System.Text;

namespace Broadcast.Shared
{
    [Serializable]
    public class Lobby
    {
        public byte broadcastVersion = 1;
        public uint id;
        public string game = string.Empty;
        public string gameVersion = string.Empty;
        public uint players = 0;
        public uint maxPlayers = 0;
        public bool requireAuth = false;
        public bool isOfficial = false;
        public string map;
        public string[] mods;
        public string strAddress = string.Empty;
        public string description = string.Empty;
        public bool isPrivate = false;
        public byte[] address = new byte[4];
        public byte[] data;
    }
}
