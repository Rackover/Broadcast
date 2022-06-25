using System;
using System.Collections.Generic;
using System.Net.Sockets;

namespace Broadcast.Shared
{
    public static class Networking
    {
        public const ushort PORT = 10000 + VERSION;
        public const byte PROTOCOL_QUERY = 0;
        public const byte PROTOCOL_SUBMIT = 1;
        public const byte PROTOCOL_DELETE = 2;
        public const byte PROTOCOL_HELLO = 3;
        public const byte PROTOCOL_REQUEST_PUNCH_TO_LOBBY = 4;
        public const byte PROTOCOL_QUERY_RESPONSE = 5;
        public const byte PROTOCOL_SUBMISSION_RESPONSE = 6;
        public const byte PROTOCOL_HELLO_PLEASE_PUNCH = 7;

        public const byte PROTOCOL_GARBAGE_PUNCH = 255;
        public const byte VERSION = 7;

        private const ushort MESSAGE_BITE_SIZE = 1024;

        public static void WriteData(this NetworkStream stream, byte[] data)
        {
            var responseList = new List<byte>();
            int size = data.Length;
            responseList.AddRange(BitConverter.GetBytes(size));
            responseList.AddRange(data);
            var toWrite = responseList.ToArray();

            lock (stream) {
                stream.Write(toWrite, 0, toWrite.Length);
            }
        }

        public static byte[] ReadUntilControllerFound(this NetworkStream stream, params byte[] controllers)
        {
            return ReadUntilControllerFound(stream, out _, controllers);
        }

        public static byte[] ReadUntilControllerFound(this NetworkStream stream, out byte controller, params byte[] controllers)
        {
            byte[] bytesRead = null;
            byte controllerFound = 0;
            stream.ReadForever((data) =>
            {
                bool shouldContinue = true;
                for (int i = 0; i < controllers.Length; i++) {
                    if (data[0] == controllers[i]) {
                        shouldContinue = false;
                        controllerFound = data[0];
                        break;
                    }
                }

                if (!shouldContinue) {
                    bytesRead = new byte[data.Length - 1];
                    Array.Copy(data, 1, bytesRead, 0, bytesRead.Length);
                }

                return shouldContinue;
            });

            controller = controllerFound;
            return bytesRead;
        }

        public delegate bool OnMessageReadDelegate(byte[] dataRead);

        /// <summary>
        /// Delegate should return true to keep reading
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="onRead"></param>
        /// <returns></returns>
        public static void ReadForever(this NetworkStream stream, OnMessageReadDelegate onRead)
        {
            var bytes = new List<byte>();
            bool gotSize = false;
            int sizeToRead = 0;

            while (true) {
                while (gotSize && bytes.Count >= sizeToRead) {
                    byte[] finalBuff = bytes.Pop(sizeToRead);

                    if (onRead.Invoke(finalBuff)) {

                        if (bytes.Count < sizeof(int)) {
                            gotSize = false;
                        }
                        else {
                            sizeToRead = bytes.PopSize();
                        }
                    }
                    else {
                        return;
                    }
                }

                var buff = new byte[MESSAGE_BITE_SIZE];
                int bytesRead;

                lock (stream) {
                    bytesRead = stream.Read(buff, 0, MESSAGE_BITE_SIZE);
                }

                byte[] trimmedBuff = new byte[bytesRead];
                Array.Copy(buff, trimmedBuff, bytesRead);
                bytes.AddRange(trimmedBuff);

                if (gotSize) {
                    // Do nothing
                }
                else {
                    // Acquire size
                    if (bytes.Count < sizeof(int)) {
                        continue;
                    }
                    else {
                        gotSize = true;
                        sizeToRead = bytes.PopSize();
                    }
                }
            }
        }

        private static int PopSize(this List<byte> buff)
        {
            byte[] intBuff = buff.Pop(sizeof(int));
            return BitConverter.ToInt32(intBuff, 0);
        }

        private static byte[] Pop(this List<byte> buff, int length)
        {
            byte[] result = new byte[length];
            for (int i = 0; i < length; i++) {
                result[i] = buff[i];
            }

            buff.RemoveRange(0, length);
            return result;
        }

        public static byte[] PrefixWith(this byte[] data, byte controller)
        {
            var final = new List<byte>(data.Length + 1);
            final.Add(controller);
            final.AddRange(data);

            return final.ToArray();
        }
    }
}
