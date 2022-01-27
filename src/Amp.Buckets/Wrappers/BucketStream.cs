using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Amp.Buckets.Wrappers
{
    public class BucketStream : Stream
    {
        bool _gotLength;
        long _length;

        public BucketStream(Bucket bucket)
        {
            Bucket = bucket?? throw new ArgumentNullException(nameof(Bucket));
        }

        public Bucket Bucket { get; }

        public override bool CanRead => true;

        public override bool CanSeek => Bucket.CanReset;

        public override bool CanWrite => false;

        public override long Length
        {
            get
            {
                if (!_gotLength)
                {
                    _gotLength = true;

                    var p = Bucket.Position;

                    if (!p.HasValue)
                        return -1L;

                    var v = Bucket.ReadRemainingBytesAsync();
                    if (!v.IsCompleted)
                        v.AsTask().Wait();

                    var r = v.Result;

                    if (r.HasValue)
                        _length = r.Value + p.Value;
                }
                return _length;
            }
        }

        public override long Position { get => Bucket.Position ?? 0L; set => Seek(value, SeekOrigin.Begin); }

        public override void Flush()
        {
            //throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var v = Bucket.ReadAsync(count);

            if (!v.IsCompleted)
                v.AsTask().Wait();

            var r = v.Result;

            if (r.IsEof)
                return 0;

            r.CopyTo(new Memory<byte>(buffer, offset, count));
            return r.Length;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var r = await Bucket.ReadAsync(count);

            if (r.IsEof)
                return 0;

            r.CopyTo(new Memory<byte>(buffer, offset, r.Length));
            return r.Length;
        }

#if !NETFRAMEWORK
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var r = await Bucket.ReadAsync(buffer.Length);

            if (r.IsEof)
                return 0;

            r.CopyTo(buffer);
            return r.Length;
        }
#endif

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new InvalidOperationException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new InvalidOperationException();
        }
    }
}
