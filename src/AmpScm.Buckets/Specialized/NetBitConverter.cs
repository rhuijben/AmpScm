using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Specialized
{
    /// <summary>
    /// Like <see cref="BitConverter"/>, but then for values in network order
    /// </summary>
    public static class NetBitConverter
    {
        public static byte[] GetBytes(short value)
        {
            return BitConverter.GetBytes(ToNetwork(value));
        }

        [CLSCompliant(false)]
        public static byte[] GetBytes(ushort value)
        {
            return BitConverter.GetBytes(ToNetwork(value));
        }

        public static byte[] GetBytes(int value)
        {
            return BitConverter.GetBytes(ToNetwork(value));
        }

        [CLSCompliant(false)]
        public static byte[] GetBytes(uint value)
        {
            return BitConverter.GetBytes(ToNetwork(value));
        }

        public static byte[] GetBytes(long value)
        {
            return BitConverter.GetBytes(ToNetwork(value));
        }

        [CLSCompliant(false)]
        public static byte[] GetBytes(ulong value)
        {
            return BitConverter.GetBytes(ToNetwork(value));
        }

        public static short FromNetwork(short value)
        {
            if (BitConverter.IsLittleEndian)
            {
                ushort val = unchecked((ushort)value);
                value = unchecked((short)((val >> 8) | (val << 8)));
            }
            return value;
        }

        [CLSCompliant(false)]
        public static ushort FromNetwork(ushort value)
        {
            if (BitConverter.IsLittleEndian)
            {
                ushort val = value;
                value = unchecked((ushort)((val >> 8) | (val << 8)));
            }
            return value;
        }

        public static int FromNetwork(int value)
        {
            if (BitConverter.IsLittleEndian)
            {
                uint val = unchecked((uint)value);
                value = unchecked((int)((val >> 24) | (val << 24) | (val & 0xFF00) << 8 | (val & 0xFF0000) >> 8));
            }
            return value;
        }

        [CLSCompliant(false)]
        public static uint FromNetwork(uint value)
        {
            if (BitConverter.IsLittleEndian)
            {
                uint val = value;
                value = unchecked((uint)((val >> 24) | (val << 24) | (val & 0xFF00) << 8 | (val & 0xFF0000) >> 8));
            }
            return value;
        }

        public static long FromNetwork(long value)
        {
            if (BitConverter.IsLittleEndian)
            {
                ulong val = unchecked((ulong)value);
                value = unchecked((long)(
                      ((val & 0x00000000000000FFL) << 56)
                    | ((val & 0x000000000000FF00L) << 40)
                    | ((val & 0x0000000000FF0000L) << 24)
                    | ((val & 0x00000000FF000000L) << 8)
                    | ((val & 0x000000FF00000000L) >> 8)
                    | ((val & 0x0000FF0000000000L) >> 24)
                    | ((val & 0x00FF000000000000L) >> 40)
                    | ((val & 0xFF00000000000000L) >> 56)));
            }
            return value;
        }

        [CLSCompliant(false)]
        public static ulong FromNetwork(ulong value)
        {
            if (BitConverter.IsLittleEndian)
            {
                ulong val = value;
                value = unchecked((ulong)(
                      ((val & 0x00000000000000FFL) << 56)
                    | ((val & 0x000000000000FF00L) << 40)
                    | ((val & 0x0000000000FF0000L) << 24)
                    | ((val & 0x00000000FF000000L) <<  8)
                    | ((val & 0x000000FF00000000L) >>  8)
                    | ((val & 0x0000FF0000000000L) >> 24)
                    | ((val & 0x00FF000000000000L) >> 40)
                    | ((val & 0xFF00000000000000L) >> 56)));
            }
            return value;
        }

        public static short ToNetwork(short value)
        {
            if (BitConverter.IsLittleEndian)
                return FromNetwork(value);
            return value;
        }

        [CLSCompliant(false)]
        public static ushort ToNetwork(ushort value)
        {
            return FromNetwork(value);            
        }

        public static int ToNetwork(int value)
        {
            return FromNetwork(value);
        }

        [CLSCompliant(false)]
        public static uint ToNetwork(uint value)
        {
            return FromNetwork(value);
        }

        public static long ToNetwork(long value)
        {
            return FromNetwork(value);
        }

        [CLSCompliant(false)]
        public static ulong ToNetwork(ulong value)
        {
            return FromNetwork(value);
        }

        public static short ToInt16(byte[] value, int startOffset)
        {
            return FromNetwork(BitConverter.ToInt16( value, startOffset));
        }

        public static int ToInt32(byte[] value, int startOffset)
        {
            return FromNetwork(BitConverter.ToInt32( value, startOffset));
        }

        public static long ToInt64(byte[] value, int startOffset)
        {
            return FromNetwork(BitConverter.ToInt64( value, startOffset));
        }

        [CLSCompliant(false)]
        public static ushort ToUInt16(byte[] value, int startOffset)
        {
            return FromNetwork(BitConverter.ToUInt16( value, startOffset));
        }

        [CLSCompliant(false)]
        public static uint ToUInt32(byte[] value, int startOffset)
        {
            return FromNetwork(BitConverter.ToUInt32( value, startOffset));
        }

        [CLSCompliant(false)]
        public static ulong ToUInt64(byte[] value, int startOffset)
        {
            return FromNetwork(BitConverter.ToUInt64( value, startOffset));
        }
    }
}
