using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace Broadcast.Shared
{
    public static class Extensions
    {
        public static bool IsSameAs(this byte[] array1, byte[] array2)
        {
            if (array1 != null && array2 != null) {
                if (array1.Length == array2.Length) {
                    for (int i = 0; i < array1.Length; i++) {
                        if (array1[i].Equals(array2[i])) {
                            continue;
                        }
                        return false;
                    }
                    return true;
                }
            }
            return false;
        }

        public static byte[] GetIPV4Addr(this IPAddress addr)
        {
            if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) {
                return addr.ToString().Split('.').Select(o=> { return Convert.ToByte(o); }).ToArray();
            }
            return null;
        } 

        public static string Format(this string str, params object[] args)
        {
            return string.Format(str, args);
        }
    }
}
