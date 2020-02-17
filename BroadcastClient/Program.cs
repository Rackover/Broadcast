using System;
using System.Net;
using System.Net.Sockets;
using Broadcast.Shared;

namespace Broadcast.Client
{
    class Program
    {
        static void Main(string[] args)
        {
            string addr = args.Length > 0 ? args[0] : "localhost";
            Client.Start(addr);
            Client.Test();
        }
    }
}
