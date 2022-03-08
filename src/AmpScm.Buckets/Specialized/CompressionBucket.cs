using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;

namespace AmpScm.Buckets.Specialized
{
    internal class CompressionBucket : WrappingBucket
    {
        private protected Stream Src { get; }
        protected Stream Processed { get; }
        byte[]? buffer;
        int _valid, _offset;
        bool _eof;

        public CompressionBucket(Bucket inner, Func<Stream, Stream> compressor) : base(inner)
        {
            Src = Inner.AsStream(new Writer(this));
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

        private class Writer : IBucketWriter
        {
            CompressionBucket Bucket { get; }

            public Writer(CompressionBucket bucket)
            {
                Bucket = bucket;
            }
            public ValueTask ShutdownAsync()
            {
                throw new NotImplementedException();
            }

            public void Write(Bucket bucket)
            {
                throw new NotImplementedException();
            }
        }
    }
}
