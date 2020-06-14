using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Linq;
using System.Threading.Tasks;

namespace Broadcast.Shared
{
    public static class Networking
    {
        public class MessageBuildingException : Exception {
            public MessageBuildingException(string msg, byte[] constructed) : base(msg + "\n" + string.Join(" ", constructed.Select(o=>o.ToString("X2")))){

            } 
        }

        public const ushort PORT = 4004;
        public const byte PROTOCOL_QUERY = 0;
        public const byte PROTOCOL_SUBMIT = 1;
        public const byte PROTOCOL_DELETE = 2;
        public const byte PROTOCOL_HELLO = 3;
        public const ushort MESSAGE_BITE_SIZE = 1024;
        public const byte VERSION = 5;


        static Dictionary<NetworkStream, bool> isStreamBusy = new Dictionary<NetworkStream, bool>();

        static bool IsStreamBusy(this NetworkStream stream)
        {
            if (!isStreamBusy.ContainsKey(stream)) {
                isStreamBusy.Add(stream, false);
            }
            return isStreamBusy[stream];
        }

        static void Lock(this NetworkStream stream)
        {
            isStreamBusy[stream] = true;
        }

        static void Unlock(this NetworkStream stream)
        {
            isStreamBusy[stream] = false;
        }

        static void WaitForStreamAvailability(this NetworkStream stream)
        {
            while (IsStreamBusy(stream)) {
                Task.WaitAny(Task.Delay(10));
            }
        }

        public static void WriteData(this NetworkStream stream, byte[] data)
        {
            var size = BitConverter.GetBytes(Convert.ToUInt32(data.Length));

            var responseList = new List<byte>();
            responseList.AddRange(size);
            responseList.AddRange(data);
            var toWrite = responseList.ToArray();

            stream.WaitForStreamAvailability();
            stream.Lock();

            stream.Write(toWrite, 0, toWrite.Length);

            stream.Unlock();
        }

        public static byte[] Read(this NetworkStream stream)
        {
            int bites = 0;
            byte[] sizeBuffer = new byte[4]; // UINT32 is 4 bytes

            stream.WaitForStreamAvailability();
            stream.Lock();

            stream.Read(sizeBuffer, 0, sizeBuffer.Length);
            
            var length = BitConverter.ToUInt32(sizeBuffer, 0);
            if (length == 0) return new byte[0]; // Garbage
            
            // Emptying network buffer from data
            var remainingData = Convert.ToInt64(length);

            List<byte> data = new List<byte>();
            while (remainingData > 0) {
                byte[] buffer = new byte[Math.Min(remainingData, MESSAGE_BITE_SIZE)];
                var dataRead = stream.Read(buffer, 0, (int)Math.Min(remainingData, MESSAGE_BITE_SIZE));
                remainingData -= dataRead;

                byte[] shrankArray = new byte[dataRead];
                Array.Copy(buffer, shrankArray, shrankArray.Length);

                data.AddRange(shrankArray);
                bites++;
            }

            stream.Unlock();


            // Concatenate data
            var msgBuffer = data.ToArray();
            if (msgBuffer.Length != length) {
                throw new MessageBuildingException(
                    string.Format("The length of the constructed message buffer is not equal to the planned length (got {0}, expected {1}). Full message below.", msgBuffer.Length, length),
                    msgBuffer
                );
            }

            return msgBuffer;
        }
    }
}
