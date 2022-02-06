using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Specialized
{
    internal class CompressionBucket : WrappingBucket
    {
        private protected SrcStream Src { get; }
        protected Stream Processed { get; }
        byte[]? buffer;
        int _valid, _offset;
        bool _eof;

        public CompressionBucket(Bucket inner, Func<Stream, Stream> compressor) : base(inner)
        {
            Src = new SrcStream(this);
            Processed = compressor(Src);
        }

        public override BucketBytes Peek()
        {
            return new BucketBytes(buffer!, _offset, _valid - _offset);
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            await Refill().ConfigureAwait(false);

            if (_valid == _offset && _eof)
                return BucketBytes.Eof;

            if (requested > _valid - _offset)
            {
                var bb = new BucketBytes(buffer!, _offset, _valid - _offset);
                _offset = _valid = 0;
                return bb;
            }
            else
            {
                var r = (int)requested;
                var bb = new BucketBytes(buffer!, _offset, r);

                _offset += r;
                if (_offset == _valid)
                    _offset = _valid = 0;
                return bb;
            }
        }

        async Task Refill()
        {
            if (buffer == null)
                buffer = new byte[4096];

            if (_offset == _valid && !_eof)
            {
#pragma warning disable CA1835 // Prefer the 'Memory'-based overloads for 'ReadAsync' and 'WriteAsync'
                int nRead = await Processed.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
#pragma warning restore CA1835 // Prefer the 'Memory'-based overloads for 'ReadAsync' and 'WriteAsync'

                if (nRead > 0)
                {
                    _offset = 0;
                    _valid = nRead;
                    return;
                }
                else
                {
                    _offset = _valid = 0;
                    _eof = true;
                }
            }
        }

        internal sealed class SrcStream : Stream
        {
            private CompressionBucket compressionBucket;
            ReadOnlyMemory<byte> remaining;

            public SrcStream(CompressionBucket compressionBucket)
            {
                this.compressionBucket = compressionBucket;
            }

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => false;

            public override long Length => throw new NotImplementedException();

            public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public override void Flush()
            {
                throw new NotImplementedException();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return ReadAsync(buffer, offset, count).Result;
            }

            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                if (remaining.IsEmpty)
                {
                    BucketBytes bb = await compressionBucket.Inner.ReadAsync(count).ConfigureAwait(false);

                    if (bb.IsEof)
                    {
                        compressionBucket._eof = true;
                        return 0;
                    }

                    remaining = bb.Memory;
                }

                if (remaining.IsEmpty)
                    return 0; // EOF

                if (count >= remaining.Length)
                {
                    remaining.Span.CopyTo(new Span<byte>(buffer, offset, remaining.Length));
                    int l = remaining.Length;
                    remaining = default;
                    return l;
                }
                else
                {
                    remaining.Span.CopyTo(new Span<byte>(buffer, offset, count));
                    remaining = remaining.Slice(count);
                    return count;
                }
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotImplementedException();
            }

            public override void SetLength(long value)
            {
                throw new NotImplementedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException();
            }
        }    
    }
}
