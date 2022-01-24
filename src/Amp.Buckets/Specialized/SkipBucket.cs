using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Amp.Buckets.Specialized
{
    interface IBucketSkip
    {
        Bucket Skip(long firstPosition);
    }

    public sealed class SkipBucket : PositionBucket, IBucketSkip
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

        public override ValueTask<BucketBytes> PeekAsync()
        {
            if (base.Position >= FirstPosition)
                return Inner.PeekAsync();
            else
                return SkipPeekAsync();
        }

        private async ValueTask<BucketBytes> SkipPeekAsync()
        {
            var b = await Inner.PeekAsync();

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

        public override ValueTask ResetAsync()
        {
            return base.ResetAsync();
        }

        private async ValueTask<BucketBytes> SkipReadAsync(int requested)
        {
            long skip = FirstPosition - base.Position!.Value;

            skip -= await ReadSkipAsync(skip);

            if (skip > 0)
                return BucketBytes.Eof;

            return await base.ReadAsync(requested);
        }

        internal static Bucket SeekOnReset(Bucket bucket)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));

            var p = bucket.Position;

            if (p.HasValue)
            {
                var sb = new SkipBucket(bucket, p.Value);
                sb.SetPosition(p.Value);
                return sb;
            }
            else
                throw new InvalidOperationException();
        }
    }
}
