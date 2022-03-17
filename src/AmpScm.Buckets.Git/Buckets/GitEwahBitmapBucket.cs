using System;
using System.Threading.Tasks;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Buckets.Git
{
    public class GitEwahBitmapBucket : GitBucket
    {
        enum ewah_state
        {
            init = 0,
            start,
            same,
            raw,
            footer,
            done
        }
        BucketBytes _readable;
        ewah_state _state;
        uint _repCount;
        int _rawCount;
        int _compressedSize;
        uint? _lengthBits;
        int _left;
        byte[] _buffer;
        int _wpos;
        bool _repBit;
        int _position;

        public GitEwahBitmapBucket(Bucket inner)
            : base(inner)
        {
            _state = ewah_state.init;
            _buffer = new byte[4096];
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            while (true)
            {
                BucketBytes bb;
                if (!_readable.IsEmpty)
                {
                    if (requested > _readable.Length)
                    {
                        bb = _readable;
                        _readable = BucketBytes.Empty;
                        _position += bb.Length;
                        return bb;
                    }

                    bb = _readable.Slice(0, requested);
                    _readable = _readable.Slice(requested);
                    _position += bb.Length;
                    return bb;
                }

                if (!await RefillAsync(true))
                    return BucketBytes.Eof;
            }
        }

        public override BucketBytes Peek()
        {
            if (_readable.IsEmpty)
                return _readable;

            RefillAsync(false).AsTask().GetAwaiter().GetResult();

            return _readable;
        }

        public async ValueTask<int> ReadBitLengthAsync()
        {
            if (_lengthBits is null)
            {
                await RefillAsync(true);
            }

            return (int)_lengthBits!.Value;
        }

        private async ValueTask<bool> RefillAsync(bool allowWait)
        {
            if (_state <= ewah_state.start && !allowWait && Inner.Peek().IsEmpty)
                return false;

            if (_lengthBits is null)
            {
                var bb = await Inner.ReadFullAsync(4 + 4);
                _lengthBits = NetBitConverter.ToUInt32(bb, 0);
                _compressedSize = NetBitConverter.ToInt32(bb, 4);

                _left = _compressedSize;
                _state = ewah_state.start;
            }

            int peekLength = Inner.Peek().Length / sizeof(ulong);
            _wpos = 0;

            switch (_state)
            {
                case ewah_state.start:
                    ulong curOp = await Inner.ReadNetworkUInt64Async();

                    _repBit = (curOp & 1UL) != 0;
                    _repCount = (uint)(curOp >> 1);
                    _rawCount = (int)(curOp >> 33);

                    _left--;
                    peekLength--;
                    _state = ewah_state.same;
                    goto case ewah_state.same;

                case ewah_state.same:
                    byte val = _repBit ? (byte)0xFF : (byte)0;
                    while (_repCount > 0 && _wpos + 8 < _buffer.Length)
                    {
                        _buffer[_wpos++] = val;
                        _buffer[_wpos++] = val;
                        _buffer[_wpos++] = val;
                        _buffer[_wpos++] = val;
                        _buffer[_wpos++] = val;
                        _buffer[_wpos++] = val;
                        _buffer[_wpos++] = val;
                        _buffer[_wpos++] = val;
                        _repCount--;
                    }
                    if (_repCount > 0)
                    {
                        _readable = new BucketBytes(_buffer, 0, _wpos);
                        return true;
                    }

                    _state = ewah_state.raw;
                    goto case ewah_state.raw;

                case ewah_state.raw:
                    while (_rawCount > 0)
                    {
                        if ((_wpos > 8 && peekLength < 8) || (_wpos + 8 >= _buffer.Length))
                        {
                            // Avoid new reads if we already have something. Return result
                            _readable = new BucketBytes(_buffer, 0, _wpos);
                            return true;
                        }

                        var bb = await Inner.ReadFullAsync(sizeof(ulong));

                        if (bb.Length != sizeof(ulong))
                            throw new BucketException("Unexpected EOF");

                        peekLength--;
                        _left--;
                        _rawCount--;

                        for (int i = bb.Length-1; i >= 0; i--)
                        {
                            _buffer[_wpos++] = bb[i];
                        }
                    }

                    if (_left == 0)
                    {
                        _state = ewah_state.footer;
                        _readable = new BucketBytes(_buffer, 0, _wpos);
                        return true;
                    }

                    _state = ewah_state.start;
                    goto case ewah_state.start;
                case ewah_state.footer:
                    await Inner.ReadNetworkUInt32Async();
                    _state = ewah_state.done;
                    goto case ewah_state.done;
                case ewah_state.done:
                default:
                    return false;
            }
        }

        public override bool CanReset => Inner.CanReset;

        public override async ValueTask ResetAsync()
        {
            await Inner.ResetAsync();
            _state = ewah_state.init;
            _wpos = 0;
            _position = 0;
        }

        public override async ValueTask<long?> ReadRemainingBytesAsync()
        {
            if (_lengthBits is null)
                await RefillAsync(true);

            return ((_lengthBits + 8 * sizeof(ulong) - 1) / (8 * sizeof(ulong))) * 8 - _position;
        }

        public override long? Position => _position;
    }
}
