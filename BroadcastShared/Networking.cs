using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace Broadcast.Shared
{
    public static class Networking
    {
        public class MessageBuildingException : Exception { 
            public MessageBuildingException(string msg) : base(msg){

            } 
        }

        public const ushort PORT = 4004;
        public const byte PROTOCOL_QUERY = 0;
        public const byte PROTOCOL_SUBMIT = 1;
        public const byte PROTOCOL_DELETE = 2;
        public const ushort MESSAGE_BITE_SIZE = 1024;
        public const byte VERSION = 4;


        public static void WriteData(this NetworkStream stream, byte[] data)
        {
            var size = BitConverter.GetBytes(Convert.ToUInt32(data.Length));
            //Array.Reverse(size);

            var responseList = new List<byte>();
            responseList.AddRange(size);
            responseList.AddRange(data);
            var toWrite = responseList.ToArray();

            stream.Write(toWrite, 0, toWrite.Length);
        }

        public static byte[] Read(this NetworkStream stream)
        {
            int bites = 0;
            byte[] sizeBuffer = new byte[4]; // UINT is 4 bytes
            stream.Read(sizeBuffer, 0, sizeBuffer.Length);

            var length = BitConverter.ToUInt32(sizeBuffer, 0);
            
            // Emptying network buffer from data
            var totalDataRead = 0;
            List<byte> data = new List<byte>();
            while (totalDataRead < length) {
                byte[] buffer = new byte[MESSAGE_BITE_SIZE];
                var dataRead = stream.Read(buffer, 0, MESSAGE_BITE_SIZE);
                totalDataRead += dataRead;

                byte[] shrankArray = new byte[dataRead];
                Array.Copy(buffer, shrankArray, shrankArray.Length);

                data.AddRange(shrankArray);
                bites++; 
            }

            // Concatenate data
            var msgBuffer = data.ToArray();
            if (msgBuffer.Length != length) {
                throw new MessageBuildingException(string.Format("The length of the constructed message buffer is not equal to the planned length (got {0}, expected {1})", msgBuffer.Length, length));
            }

            return msgBuffer;
        }
    }
}
