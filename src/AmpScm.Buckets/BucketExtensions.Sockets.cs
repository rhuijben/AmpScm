using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Buckets
{
    partial class BucketExtensions
    {
#if NETFRAMEWORK && !NET48_OR_GREATER
        internal static Task<int> ReceiveAsync(this Socket socket, Memory<byte> buffer, SocketFlags socketFlags)
        {
            if (socket == null)
                throw new ArgumentNullException(nameof(socket));

            return Task<int>.Factory.FromAsync(
                (AsyncCallback cb, object? state) =>
                {
                    var (arr, offset) = BucketBytes.ExpandToArray(buffer);

                    return socket.BeginReceive(arr!, offset, buffer.Length, socketFlags, cb, state);
                },
                socket.EndReceive, null);
        }


        internal static Task<int> SendAsync(this Socket socket, ReadOnlyMemory<byte> buffer, SocketFlags socketFlags)
        {
            if (socket == null)
                throw new ArgumentNullException(nameof(socket));

            return Task<int>.Factory.FromAsync(
                (AsyncCallback cb, object? state) =>
                {
                    var (arr, offset) = BucketBytes.ExpandToArray(buffer);

                    return socket.BeginSend(arr!, offset, buffer.Length, socketFlags, cb, state);
                },
                socket.EndReceive, null);
        }

        internal static Task ConnectAsync(this Socket socket, string host, int port)
        {
            if (socket == null)
                throw new ArgumentNullException(nameof(socket));

            return Task.Factory.FromAsync(
                (AsyncCallback cb, object? state) => socket.BeginConnect(host, port, cb, state),
                socket.EndConnect, null);
        }
#endif
    }

}

