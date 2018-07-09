using System;
using System.Collections.Generic;
using System.Text;

namespace SharpLink
{
    internal class Util
    {
        internal static ushort SwapEndianess(ushort value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes);
            return BitConverter.ToUInt16(bytes, 0);
        }

        internal static ulong SwapEndianess(ulong value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes);
            return BitConverter.ToUInt64(bytes, 0);
        }
    }
}
