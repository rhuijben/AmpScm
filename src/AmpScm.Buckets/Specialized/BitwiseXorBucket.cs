using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Specialized
{
    public class BitwiseXorBucket : CombineBucket
    {
        readonly byte[] _buffer;
        BucketBytes _bbLeft;
        BucketBytes _bbRight;
        
        public BitwiseXorBucket(Bucket left, Bucket right) : base(left, right)
        {
            _buffer = new byte[4096];
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            if (!_bbLeft.IsEmpty || !_bbRight.IsEmpty)
            {
                if (_bbLeft.IsEmpty)
                    _bbLeft = await Left.ReadAsync(Math.Max(_bbRight.Length, 1)).ConfigureAwait(false);

                if (_bbRight.IsEmpty)
                    _bbRight = await Right.ReadAsync(Math.Max(_bbRight.Length, 1)).ConfigureAwait(false);
            }
            else
            {
                _bbLeft = await Left.ReadAsync(Math.Min(_buffer.Length, requested)).ConfigureAwait(false);
                _bbRight = await Right.ReadAsync(Math.Max(_bbLeft.Length, 1)).ConfigureAwait(false);
            }

            if (_bbLeft.IsEof)
            {
                if (!_bbRight.IsEof)
                    throw new BucketException($"Left stream of {Name} got EOF before right stream");
                else
                    return BucketBytes.Eof;
            }
            else if (_bbRight.IsEof)
                throw new BucketException($"Right stream of {Name} got EOF before right stream");

            int got = Process();

            if (got == _bbLeft.Length)
                _bbLeft = BucketBytes.Empty;
            else
                _bbLeft = _bbLeft.Slice(got);

            if (got == _bbRight.Length)
                _bbRight = BucketBytes.Empty;
            else
                _bbRight = _bbRight.Slice(got);

            return new BucketBytes(_buffer, 0, got);
        }

        public override BucketBytes Peek()
        {
            // TODO: Check if both sides have something to peek
            return base.Peek();
        }

        public override ValueTask<int> ReadSkipAsync(int requested)
        {
            // TODO: Skip on both sides
            return base.ReadSkipAsync(requested);
        }

        int Process()
        {
            int got = Math.Min(_bbLeft.Length, _bbRight.Length);

            // TODO: Optimize with vector operations...
            for (int i = 0; i < got; i++)
                _buffer[i] = (byte)(_bbLeft[i] ^ _bbRight[i]);

            return got;
        }

        public override string Name => $"{BaseName}>[{Left.Name}],[{Right.Name}]";

        public override bool CanReset => Left.CanReset && Right.CanReset;

        public override async ValueTask ResetAsync()
        {
            await Left.ResetAsync().ConfigureAwait(false);
            await Right.ResetAsync().ConfigureAwait(false);

            _bbLeft = _bbRight = BucketBytes.Empty;
        }

        public override long? Position => Left.Position;

        public override async ValueTask<long?> ReadRemainingBytesAsync()
        {
            var l1 = await Left.ReadRemainingBytesAsync().ConfigureAwait(false);
            var l2 = await Right.ReadRemainingBytesAsync().ConfigureAwait(false);

            if (l1 == l2)
                return l1;
            else
                return null;
        }
    }
}
