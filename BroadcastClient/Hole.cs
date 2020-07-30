using Broadcast.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Broadcast.Client
{
    public static class Hole
    {
        public async static Task PunchUDP(string ipAddress, ushort port, Logger logger)
        {
            while (true)
            {
                try
                {
                    using (UdpClient c = new UdpClient(port))
                    {
                        c.Send(Enumerable.Range(0, 255).Select(o => (byte)Networking.PROTOCOL_GARBAGE_PUNCH).ToArray(), 255, ipAddress, port);
                        logger.Info("Finished punching " + ipAddress + ":" + port);
                        break;
                    }
                }
                catch (SocketException)
                {
                    // Do nothing, wait for next opportunity
                    logger.Trace("Could not punch {0}:{1}, waiting for next opportunity".Format(ipAddress, port));
                    await Task.Delay(100);
                }
            }
        }
    }
}
