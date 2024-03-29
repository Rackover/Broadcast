﻿using System;
using Broadcast.Server;
using Broadcast.Client;
using System.Threading.Tasks;

namespace Broadcast.Tester
{
    class Program
    {
        const int CLIENTS = 50;

        static void Main(string[] args)
        {
            new Task(delegate {
                new Server.Server();
            }).Start();

            new Client.Client("localhost", "test").Test().Wait();
        }
    }
}
