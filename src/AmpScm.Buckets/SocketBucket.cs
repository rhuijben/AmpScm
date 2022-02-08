using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Buckets
{
    public class SocketBucket : Bucket, IBucketWriter
    {
        byte[] _inputBuffer;
        BucketBytes _unread;
        bool _readEof, _writeEof;
        Task? _writing;
        private protected Socket Socket { get; }

        internal WaitForDataBucket WriteBucket { get; } = new WaitForDataBucket();

        public SocketBucket(Socket socket, int bufferSize = 16384)
        {
            Socket = socket ?? throw new ArgumentNullException(nameof(socket));
            _inputBuffer = new byte[bufferSize];
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                    Socket.Dispose();
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        protected override async ValueTask DisposeAsyncCore()
        {
            await base.DisposeAsyncCore().ConfigureAwait(false);
            Socket.Dispose();
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            Task<BucketBytes> reading = DoRead(requested);
            Task ready;
            do
            {
                if (_writeEof)
                    break; // Use wait at return for reading
                
                _writing ??= HandleWriting();

                ready = await Task.WhenAny(reading, _writing).ConfigureAwait(false);

                if (ready == _writing)
                    _writing = null;
            }
            while (ready != reading);

            return await reading.ConfigureAwait(false);
        }

        public override BucketBytes Peek()
        {
            return _unread;
        }

        async Task<BucketBytes> DoRead(int requested)
        {
            if (_unread.Length > 0)
            {
                var bb = _unread.Slice(0, Math.Min(requested, _unread.Length));
                _unread = _unread.Slice(bb.Length);
                return bb;
            }
            else if (_readEof)
                return BucketBytes.Eof;

            Memory<byte> buffer = new Memory<byte>(_inputBuffer);
            int len = await Socket.ReceiveAsync(new ArraySegment<byte>(_inputBuffer), SocketFlags.None).ConfigureAwait(false);

            if (len > 0)
            {
                if (len > requested)
                {
                    _unread = new BucketBytes(_inputBuffer, requested, len - requested);
                    return new BucketBytes(_inputBuffer, 0, requested);
                }
                else
                {
                    return new BucketBytes(_inputBuffer, 0, len);
                }
            }
            else
            {
                _readEof = true;
                return BucketBytes.Eof;
            }
        }

        async Task HandleWriting()
        {
            while(true)
            {
                var bb = await WriteBucket.ReadAsync().ConfigureAwait(false);

                if (bb.IsEof)
                {
                    if (!_writeEof)
                    {
                        _writeEof = true;
                        Socket.Shutdown(SocketShutdown.Send);
                    }
                }

                while(bb.Length > 0)
                {
                    var (arr, offs) = bb.ExpandToArray();

                    int written = await Socket.SendAsync(new ArraySegment<byte>(arr!, offs, bb.Length), SocketFlags.None).ConfigureAwait(false);

                    if (written > 0)
                        bb = bb.Slice(written);
                    else
                        return;
                }
            }
        }

        public void Write(Bucket bucket)
        {
            WriteBucket.Write(bucket);
        }

        public ValueTask ShutdownAsync()
        {
            return WriteBucket.ShutdownAsync();
        }
    }
}
