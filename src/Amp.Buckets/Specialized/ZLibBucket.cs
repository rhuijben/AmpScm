using ComponentAce.Compression.Libs.zlib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Amp.Buckets.Specialized
{
    public enum ZLibLevel
    {
        Store = zlibConst.Z_NO_COMPRESSION,
        BestSpeed = zlibConst.Z_BEST_SPEED,
        Maximum = zlibConst.Z_BEST_COMPRESSION
    }

    public sealed class ZLibBucket : WrappingBucket
    {
        readonly ZStream _z;
        bool _eof, _readEof;
        BucketBytes read_buffer;
        BucketBytes write_buffer;
        byte[] write_data;
        long _position;
        int? _windowBits;
        ZLibLevel? _level;

        public ZLibBucket(Bucket inner)
            : this(inner, 15 /* 15 for zlib. -15 for deflate and 31 for gzip */)
        {
        }

        private ZLibBucket(Bucket inner, int windowBits)
            : base(inner)
        {
            _z = new ZStream();
            _z.inflateInit(windowBits);
            write_data = new byte[8192];
            _windowBits = windowBits;
        }

        public ZLibBucket(Bucket inner, ZLibLevel level)
            : base(inner)
        {
            _z = new ZStream();
            _z.deflateInit((int)level);
            write_data = new byte[8192];
            _level = level;
        }

        public override string Name => "ZLib/" + Inner.Name;

        async ValueTask<bool> Refill(bool forPeek)
        {
            bool retry_refill;
            do
            {
                retry_refill = false;
                bool did_peek = false;

                if (!_readEof && read_buffer.IsEmpty)
                {
                    var bb = await Inner.PeekAsync();

                    if (bb.IsEmpty)
                    {
                        if (forPeek)
                            return false; // Not at EOF, not filled

                        bb = await Inner.ReadAsync(1);

                        if (bb.Length == 0)
                        {
                            System.Diagnostics.Debug.Assert(bb.IsEof);
                            _readEof = true;
                            read_buffer = Array.Empty<byte>();
                        }
                        else
                        {
                            read_buffer = bb;
                        }
                    }
                    else
                    {
                        read_buffer = bb;
                        did_peek = true;
                    }
                }
                else
                {
                    GC.KeepAlive(read_buffer);
                }

                var (rb, rb_offs, rb_len) = read_buffer.ExpandToArray();

                _z.next_in = rb;
                _z.next_in_index = rb_offs;
                _z.avail_in = rb_len;

                _z.next_out = write_data;
                _z.next_out_index = 0;
                _z.avail_out = write_data.Length;

                int r = _z.inflate(_readEof ? zlibConst.Z_FINISH : zlibConst.Z_SYNC_FLUSH); // Write as much inflated data as possible

                write_buffer = new BucketBytes(write_data, 0, _z.next_out_index);
                int to_read = _z.next_in_index - rb_offs;
                read_buffer = read_buffer.Slice(_z.next_in_index - rb_offs);

                if (r == zlibConst.Z_STREAM_END)
                {
                    _readEof = true;
                    _eof = true;
                }
                //else if (r == zlibConst.Z_BUF_ERROR && _readEof && _z.next_out_index == 0)
                //{
                //    _eof = true;
                //}
                else if (r != zlibConst.Z_OK)
                {
                    throw new System.IO.IOException($"ZLib inflate failed {r}: {_z.msg}");
                }

                if (write_buffer.IsEmpty)
                    retry_refill = true;

                if (did_peek)
                {
                    // We only peeked the data, and performed no actual read. Let's perform the requested read now
                    read_buffer = BucketBytes.Empty; // Need to re-peek next time

                    var ar = Inner.ReadAsync(to_read);
                    if (!ar.IsCompleted || ar.Result.Length != to_read)
                    {
                        ar.AsTask().Wait(); // Should never happen when peek succeeds.

                        if (ar.Result.Length != to_read)
                            throw new InvalidOperationException("Read did not complete as promissed by peek");
                        else
                            System.Diagnostics.Trace.WriteLine($"Peek of {Inner.GetType()} promised data that read couldn't deliver without waiting");
                    }
                }
            }
            while (retry_refill && !_eof);

            return _eof && write_buffer.IsEmpty;
        }

        public async override ValueTask<BucketBytes> PeekAsync()
        {
            if (_eof)
                return BucketBytes.Empty;

            await Refill(true);

            return write_buffer;
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            if (write_buffer.IsEmpty)
            {
                if (_eof || await Refill(false))
                    return BucketBytes.Eof;
            }

            if (requested > write_buffer.Length)
                requested = write_buffer.Length;

            var bb = write_buffer.Slice(0, requested);
            write_buffer = write_buffer.Slice(requested);
            _position += requested;

            System.Diagnostics.Debug.Assert(bb.Length > 0);
            return bb;
        }

        public override bool CanReset => Inner.CanReset;

        public override long? Position => _position;

        public override async ValueTask ResetAsync()
        {
            if (!CanReset)
                throw new InvalidOperationException();

            await Inner.ResetAsync();

            if (_windowBits is int wb)
                _z.inflateInit(wb);
            else if (_level is ZLibLevel zl)
                _z.deflateInit((int)zl);

            _eof = _readEof = false;
            write_buffer = BucketBytes.Empty;
            _position = 0;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                write_data = null!;
                read_buffer = default;
                write_buffer = default;
            }
        }

        protected override ValueTask DisposeAsyncCore()
        {
            write_data = null!;
            read_buffer = default;
            write_buffer = default;

            return base.DisposeAsyncCore();
        }

        public override async ValueTask<Bucket> DuplicateAsync(bool reset)
        {
            if (!reset)
                throw new InvalidOperationException();

            var b = await Inner.DuplicateAsync(reset);

            if (_windowBits is int wb)
                return new ZLibBucket(b);
            else if (_level is ZLibLevel zl)
                return new ZLibBucket(b, zl);
            else
                throw new InvalidOperationException();
        }
    }
}
