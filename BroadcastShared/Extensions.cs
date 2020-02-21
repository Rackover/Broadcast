using System;
using System.Collections.Generic;
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
    }
}
