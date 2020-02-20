using System;
using Broadcast.Server;
using Broadcast.Client;
using System.Threading.Tasks;

namespace Broadcast.Tester
{
    class Program
    {
        static void Main(string[] args)
        {
            new Task(delegate {
                new Server.Server();
            }).Start();
            Client.Client.Start("localhost", "test");
            Client.Client.Test();
        }
    }
}
