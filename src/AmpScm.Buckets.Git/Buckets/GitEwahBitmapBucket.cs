using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Buckets.Git.Buckets
{
    public class GitEwahBitmapBucket : GitBucket
    {
        int _state;
        uint _position;
        uint _compressedSize;
        uint _lengthBits;
        byte[] _buffer;
        BucketBytes _remaining;
        ulong _op;
        long _bitsLeft;
        uint _wpos;

        public GitEwahBitmapBucket(Bucket inner)
            : base(inner)
        {
            _state = -8; // Need 8 bytes before starting
            _buffer = new byte[4096];
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            if (_state < 0)
            {
                int rq = -_state;
                var bb = await Inner.ReadFullAsync(rq);
                var info = bb.ToArray();

                _lengthBits = NetBitConverter.ToUInt32(info, 0);
                _compressedSize = NetBitConverter.ToUInt32(info, 4);
                _state = 0;
            }

            await Refill(true);

            if (_remaining.Length > 0)
            {
                if (_remaining.Length >= requested)
                {
                    var r = _remaining;
                    _remaining = BucketBytes.Empty;
                    return r;
                }
                else
                {
                    var r = _remaining.Slice(0, requested);
                    _remaining = _remaining.Slice(requested);
                    return r;
                }
            }
            else
                return BucketBytes.Empty;
        }

        public override BucketBytes Peek()
        {
            if (_remaining.Length > 0)
                return _remaining;

            Refill(false).AsTask().GetAwaiter().GetResult();

            return _remaining;
        }

        void WriteBit(bool on)
        {
            if (on)
                _buffer[_wpos >> 3] |= (byte)(1 << (7 - ((int)_wpos & 0x7)));
            _wpos++;
        }

        private ValueTask Refill(bool allowWait)
        {
            if (_state < 0)
                return default;

            if (_remaining.Length > 0)
                return default;

            while (_wpos / 8 < _buffer.Length)
            {
                // Align written bits to byte boundary
                while (((_wpos & 0x7) != 0) && _bitsLeft > 7 && _wpos / 8 < _buffer.Length)
                {
                    WriteBit((_op & 0x8000000000000000) != 0UL);
                    _bitsLeft--;
                }
                // Write all bytes with same value
                while ((_bitsLeft >= 7 + 8) && _wpos / 8 < _buffer.Length)
                {
                    _buffer[_wpos / 8] = ((_op & 0x8000000000000000) != 0) ? (byte)0xFF : (byte)0;
                    _wpos += 8;
                    _bitsLeft -= 8;
                }
                // Write remaining bits
                while (((_wpos & 0x7) != 0) && _bitsLeft > 7 && _wpos / 8 < _buffer.Length)
                {
                    WriteBit((_op & 0x8000000000000000) != 0UL);
                    _bitsLeft--;
                }

                if (_wpos / 7 >= _buffer.Length)
                {
                    _remaining = _buffer;
                    return default;
                }

                //throw new NotImplementedException();
                return default;
            }

            return default;
        }

        public override bool CanReset => Inner.CanReset;

        public override async ValueTask ResetAsync()
        {
            await Inner.ResetAsync();
            _state = -8;
        }

        public override async ValueTask<long?> ReadRemainingBytesAsync()
        {
            return (_state < 0) ? null : (_compressedSize + 7) / 8 - _position;
        }
    }
}
