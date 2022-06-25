using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Net;      //required
using System.Net.Sockets;    //required
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

using Broadcast.Shared;

namespace Broadcast.Server
{
    class Program
    {

        static void Main(string[] args)
        {
            new Server();
        }
    }
}