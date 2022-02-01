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

        public override async ValueTask<BucketBytes> PeekAsync()
        {
            await Refill();

            return new BucketBytes(buffer!, _offset, _valid - _offset);
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            await Refill();

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
                int nRead = await Processed.ReadAsync(buffer, 0, buffer.Length);

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
                if (remaining.IsEmpty)
                {
                    var v = compressionBucket.Inner.ReadAsync(count);
                    BucketBytes bb;

                    if (v.IsCompleted)
                        bb = v.Result;
                    else
                        bb = v.GetAwaiter().GetResult();

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

            public async override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                if (remaining.IsEmpty)
                {
                    var bb = await compressionBucket.Inner.ReadAsync(count);

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

        /// <summary>
        /// The DeflateBucket can do the hard work, but we don't want to overshoot reading, so we have to do
        /// some block reading magic
        /// 
        /// </summary>
        internal class ZLibBucket : WrappingBucket
        {
            int _nSkipped;

            public ZLibBucket(Bucket inner) : base(inner)
            {
            }

            public override ValueTask<BucketBytes> PeekAsync()
            {
                throw new NotImplementedException();
            }

            public async override ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
            {
                while (_nSkipped < 2)
                {
                    var s = await Inner.ReadAsync(2 - _nSkipped);

                    if (s.IsEof)
                        return s;

                    _nSkipped += s.Length;
                }

                return await Inner.ReadAsync(requested);
            }
        }
    }
}
#if ERR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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

        public override async ValueTask<BucketBytes> PeekAsync(bool noPoll = false)
        {
            await Refill();

            return new BucketBytes(buffer!, _offset, _valid - _offset);
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            await Refill();

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
                int nRead = await Processed.ReadAsync(buffer, 0, buffer.Length);

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
                if (remaining.IsEmpty)
                {
                    var v = compressionBucket.Inner.ReadAsync(count);
                    BucketBytes bb;

                    if (v.IsCompleted)
                        bb = v.Result;
                    else
                        bb = v.GetAwaiter().GetResult();

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

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                return base.ReadAsync(buffer, offset, count, cancellationToken);
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

        /// <summary>
        /// The DeflateBucket can do the hard work, but we don't want to overshoot reading, so we have to do
        /// some block reading magic
        /// 
        /// </summary>
        internal class ZLibBucket : WrappingBucket
        {
            int _nSkipped;
            ZState _state;
            byte b0, b1;
            enum ZState
            {
                init0 = 0,
                init1,
                head,
                blockstart,
                inblock,
                eof
            }

            public ZLibBucket(Bucket inner) : base(inner)
            {
            }

            public override ValueTask<BucketBytes> PeekAsync(bool noPoll = false)
            {
                return BucketBytes.Empty;
            }

            public async override ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
            {
                if (_state == ZState.eof)
                    return BucketBytes.Eof;

                var peek = await Inner.PeekAsync(false);
                int nRead = 0;

                while (true)
                {
                    if (peek.Length == 0)
                    {
                        peek = await Inner.ReadAsync(1);

                        if (peek.Length == 1)
                            nRead = 1;
                        else if (peek.IsEof)
                        {
                            _state = ZState.eof;
                            return BucketBytes.Eof;
                        }
                        nRead = -1;
                    }

                    while (!peek.IsEmpty)
                    {
                        switch (_state)
                        {
                            case ZState.init0:
                                b0 = peek.Span[0];
                                peek = peek.Slice(1);
                                nRead++;

                                _state = ZState.init1;
                                break;

                            case ZState.init1:
                                b1 = peek.Span[0];
                                peek = peek.Slice(1);
                                nRead++;

                                _state = ZState.head;
                                break;

                            case ZState.head:
                                byte bh = peek.Span[0];

                                if ((bh & 0x01) == 1)
                                {
                                    nRead++;
                                    _state = ZState.eof;
                                    break;
                                }
                                switch ((bh & 0x06))
                                {
                                    case 0x00:
                                        break;
                                    case 0x02:
                                        break;
                                    case 0x04:
                                        break;
                                    case 0x06:
                                        break;
                                }
                                break;
                        }
                    }

                    if (nRead > 0)
                    {
                        var r = await Inner.ReadAsync(nRead);
                        nRead -= r.Length;
                    }


                    return await Inner.ReadAsync(requested);
                }
            }
        }
    }
}
#endif