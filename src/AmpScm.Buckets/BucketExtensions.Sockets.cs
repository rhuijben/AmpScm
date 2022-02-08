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
        public static async ValueTask<int> ReceiveAsync(this Socket socket, Memory<byte> buffer, SocketFlags socketFlags, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (socket == null)
                throw new ArgumentNullException(nameof(socket));

            var tcs = new TaskCompletionSource<int>(socket);

            var (arr, offset) = BucketBytes.ExpandToArray(buffer);

            socket.BeginReceive(arr!, offset, buffer.Length, socketFlags, iar =>
            {
                var t = (TaskCompletionSource<int>)iar.AsyncState!;
                var s = (Socket)t.Task.AsyncState!;
                try 
                { 
                    t.TrySetResult(s.EndReceive(iar)); 
                }
                catch (Exception exc) 
                { 
                    t.TrySetException(exc); 
                }
            }, tcs);
            return await tcs.Task.ConfigureAwait(false);
        }


        public static async ValueTask<int> SendAsync(this Socket socket, ReadOnlyMemory<byte> buffer, SocketFlags socketFlags, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (socket == null)
                throw new ArgumentNullException(nameof(socket));

            var tcs = new TaskCompletionSource<int>(socket);

            var (arr, offset) = BucketBytes.ExpandToArray(buffer);

            socket.BeginSend(arr!, offset, buffer.Length, socketFlags, iar =>
            {
                var t = (TaskCompletionSource<int>)iar.AsyncState!;
                var s = (Socket)t.Task.AsyncState!;
                try
                {
                    t.TrySetResult(s.EndSend(iar));
                }
                catch (Exception exc)
                {
                    t.TrySetException(exc);
                }
            }, tcs);
            return await tcs.Task.ConfigureAwait(false);
        }

        public static async ValueTask ConnectAsync(this Socket socket, string host, int port, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (socket == null)
                throw new ArgumentNullException(nameof(socket));

            var tcs = new TaskCompletionSource<bool>(socket);

            socket.BeginConnect(host, port, iar =>
            {
                var t = (TaskCompletionSource<bool>)iar.AsyncState!;
                var s = (Socket)t.Task.AsyncState!;
                try
                {
                    s.EndConnect(iar);
                    t.TrySetResult(true);
                }
                catch (Exception exc)
                {
                    t.TrySetException(exc);
                }
            }, tcs);
            await tcs.Task.ConfigureAwait(false);
        }
#endif
    }

}

