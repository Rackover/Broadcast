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
        const byte HOLES_COUNT = 10;

        public async static Task PunchUDP(string ipAddress, ushort port, Logger logger)
        {
            while (true)
            {
                try
                {
                    using (Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                    {
                        sock.Connect(ipAddress, port);
                        for (int i = 0; i < HOLES_COUNT; i++)
                        {
                            logger.Trace("Punching " + ipAddress + ":" + port+" ("+i+"/"+HOLES_COUNT+")");

                            // Create a 255-filled packet
                            sock.Send(Enumerable.Range(0, 255).Select(o => (byte)Networking.PROTOCOL_GARBAGE_PUNCH).ToArray());

                            await Task.Delay(100);
                        }
                        logger.Info("Finished punching " + ipAddress + ":" + port);
                        break;
                    }
                }
                catch (SocketException e)
                {
                    // Do nothing, wait for next opportunity
                    logger.Trace("Could not punch {0}:{1}, waiting for next opportunity ({2})".Format(ipAddress, port, e.ToString()));
                    await Task.Delay(100);
                }
            }
        }
    }
}
