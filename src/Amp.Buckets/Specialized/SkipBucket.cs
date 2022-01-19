using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Amp.Buckets.Specialized
{
    public sealed class SkipBucket : PositionBucket
    {
        public long FirstPosition { get; private set; }

        public SkipBucket(Bucket inner, long firstPosition) : base(inner)
        {
            if (firstPosition < 0)
                throw new ArgumentOutOfRangeException(nameof(firstPosition));

            FirstPosition = firstPosition;
        }

        public Bucket Skip(long firstPosition)
        {
            if (firstPosition < 0)
                throw new ArgumentOutOfRangeException(nameof(firstPosition));

            if (Position == 0)
            {
                FirstPosition += firstPosition;
                return this;
            }
            else
                return new SkipBucket(this, firstPosition);
        }

        public override long? Position => Math.Max(0L, base.Position!.Value - FirstPosition);

        public override ValueTask<BucketBytes> PeekAsync(bool noPoll = false)
        {
            if (base.Position >= FirstPosition)
                return Inner.PeekAsync(noPoll);
            else
                return SkipPeekAsync(noPoll);
        }

        private async ValueTask<BucketBytes> SkipPeekAsync(bool noPoll)
        {
            var b = await Inner.PeekAsync(noPoll);

            if (b.Length > 0)
            {
                long skip = FirstPosition - base.Position!.Value;

                if (skip < b.Length)
                    return b.Slice((int)skip);
                else
                    return BucketBytes.Empty;
            }
            else
                return BucketBytes.Empty;
        }

        public override ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            if (base.Position >= FirstPosition)
                return base.ReadAsync(requested);
            else
                return SkipReadAsync(requested);
        }

        private async ValueTask<BucketBytes> SkipReadAsync(int requested)
        {
            long skip = FirstPosition - base.Position!.Value;

            while (skip + requested > int.MaxValue)
            {
                var r = await base.ReadSkipAsync(requested);
                if (r == 0)
                    return BucketBytes.Eof;

                skip = FirstPosition - base.Position!.Value;

            }

            requested += (int)skip;

            var b = await base.ReadAsync(requested);

            if (b.Length > 0)
            {
                if (skip < b.Length)
                    return b.Slice((int)skip);
                else
                    return BucketBytes.Empty;
            }
            else
                return BucketBytes.Empty;
        }
    }
}
