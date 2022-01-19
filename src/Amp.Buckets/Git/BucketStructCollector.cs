using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Amp.Buckets.Git
{
    [AttributeUsage(AttributeTargets.Field)]
    sealed class NetworkOrderAttribute : Attribute
    {
        internal static int ToHost(int value)
        {
            return unchecked((int)ToHost((uint)value));
        }

        internal static uint ToHost(uint value)
        {
            if (BitConverter.IsLittleEndian)
            {
                // TODO: Optimize
                return BitConverter.ToUInt32(BitConverter.GetBytes(value).Reverse().ToArray(), 0);
            }
            return value;
        }

        internal static long ToHost(long value)
        {
            return unchecked((long)ToHost((ulong)value));
        }

        internal static ulong ToHost(ulong value)
        {
            if (BitConverter.IsLittleEndian)
            {
                // TODO: Optimize
                return BitConverter.ToUInt64(BitConverter.GetBytes(value).Reverse().ToArray(), 0);
            }
            return value;
        }

        internal static short ToHost(short value)
        {
            return unchecked((short)ToHost((short)value));
        }

        internal static ushort ToHost(ushort value)
        {
            if (BitConverter.IsLittleEndian)
            {
                // TODO: Optimize
                return BitConverter.ToUInt16(BitConverter.GetBytes(value).Reverse().ToArray(), 0);
            }
            return value;
        }
    }

    [DebuggerDisplay("Result={Result}")]
    internal class BucketStructCollector<TRead> where TRead : struct
    {
        object? _state;
        int _pos;

        public async ValueTask<ValueOrEof<TRead>> ReadAsync(Bucket b)
        {
            if (_state is TRead r)
                return r;

            byte[] bytes = (byte[])_state!;
            if (bytes == null)
                _state = bytes = new byte[Marshal.SizeOf(typeof(TRead))];

            do
            {
                var rd = await b.ReadAsync(bytes.Length - _pos);

                if (rd.Length > 0)
                {
                    rd.CopyTo(new Memory<byte>(bytes, _pos, bytes.Length - _pos));
                    _pos += rd.Length;
                }
                else if (rd.IsEof)
                    return new ValueOrEof<TRead>(true);
            }
            while(_pos < bytes.Length);

            var v = LoadStruct(bytes);
            _state = v;

            return v;
        }

        unsafe TRead LoadStruct(byte[] bytes)
        {
            object r;
            fixed (byte* pData = &bytes[0])
            {
                r = Marshal.PtrToStructure<TRead>((IntPtr)pData);
            }

            foreach(var f in typeof(TRead).GetFields())
            {
                if (f.GetCustomAttributes<NetworkOrderAttribute>()?.Any() ?? false)
                {
                    var dd = f.GetValue(r);

                    if (dd is int di)
                        dd = NetworkOrderAttribute.ToHost(di);
                    else if (dd is long dl)
                        dd = NetworkOrderAttribute.ToHost(dl);
                    else if (dd is short ds)
                        dd = NetworkOrderAttribute.ToHost(ds);
                    else if (dd is uint dui)
                        dd = NetworkOrderAttribute.ToHost(dui);
                    else if (dd is ulong dul)
                        dd = NetworkOrderAttribute.ToHost(dul);
                    else if (dd is short dus)
                        dd = NetworkOrderAttribute.ToHost(dus);
                    else
                        continue;

                    f.SetValue(r, dd);
                }
            }
            return (TRead)r;
        }

        public TRead? Result => _state as TRead?;

        public bool HasResult => _state is TRead;
    }
}
