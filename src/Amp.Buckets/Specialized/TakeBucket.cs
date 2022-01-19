using Amp.Buckets.Specialized;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Amp.Buckets.Specialized
{
    public sealed class TakeBucket : PositionBucket
    {
        public long Limit { get; private set; }

        public TakeBucket(Bucket inner, long limit)
            : base(inner)
        {
            if (limit < 0)
                throw new ArgumentOutOfRangeException(nameof(limit));

            Limit = limit;
        }

        public Bucket Take(long limit)
        {
            if (limit < 0)
                throw new ArgumentOutOfRangeException(nameof(limit));

            if (limit < Limit)
                Limit = limit;

            return this;
        }

        public override async ValueTask<BucketBytes> PeekAsync(bool noPoll = false)
        {
            var peek = await base.PeekAsync(noPoll);

            if (peek.Length <= 0)
                return peek;

            long pos = Position!.Value;

            if (Limit - pos < peek.Length)
                return peek.Slice(0, (int)(Limit - pos));

            return peek;
        }

        public override ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            long pos = Position!.Value;

            if (pos >= Limit)
                return BucketBytes.Eof;

            if (Limit - pos < requested)
                requested = (int)(Limit - pos);

            return base.ReadAsync(requested); // Position updated in base
        }

        public override ValueTask<int> ReadSkipAsync(int requested)
        {
            long pos = Position!.Value;

            if (pos >= Limit) return new ValueTask<int>(0);

            if (Limit - pos < requested)
                requested = (int)(Limit - pos);

            return base.ReadSkipAsync(requested);
        }

        public async override ValueTask<long?> ReadRemainingBytesAsync()
        {
            long pos = Position!.Value;

            if (pos >= Limit)
                return 0L;

            var limit = Limit - pos;
            var l = await base.ReadRemainingBytesAsync();

            if (!l.HasValue)
                return null;

            return Math.Min(limit, l.Value);
        }

        protected override PositionBucket NewPositionBucket(Bucket duplicatedInner)
        {
            return new TakeBucket(duplicatedInner, Limit);
        }
    }
}
