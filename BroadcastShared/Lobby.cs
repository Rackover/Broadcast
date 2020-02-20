using MessagePack;
using System;
using System.Collections.Generic;
using System.Text;

namespace Broadcast.Shared
{
    [Serializable]
    [MessagePackObject]
    public class Lobby
    {
        [Key(0)] public readonly byte broadcastVersion = Networking.VERSION;
        [Key(1)] public string game = string.Empty;
        [Key(2)] public uint id;

        [Key(3)] public string secretKey = string.Empty;
        [Key(4)] public string gameVersion = string.Empty;
        [Key(5)] public uint players = 0;
        [Key(6)] public uint maxPlayers = 0;
        [Key(7)] public bool requireAuth = false;
        [Key(8)] public bool isOfficial = false;
        [Key(9)] public string map;
        [Key(10)] public string[] mods;
        [Key(11)] public string title = string.Empty;
        [Key(12)] public string description = string.Empty;
        [Key(13)] public bool isPrivate = false;

        [Key(14)] public byte[] address = new byte[4];
        [Key(15)] public string strAddress = string.Empty;
        [Key(16)] public ushort port = 1;
        [Key(17)] public EProtocol protocol = EProtocol.IPv4;

        [Key(18)] public byte[] data;
    }
}
