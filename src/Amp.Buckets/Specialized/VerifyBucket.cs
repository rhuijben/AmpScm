using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Amp.Buckets.Specialized
{
    // Marker only interface
    interface IBucketVerify
    {

    }

    sealed class VerifyBucket<TBucket> : ProxyBucket<VerifyBucket<TBucket>>, IBucketAggregation, IBucketVerify
        where TBucket : Bucket
    {
        public VerifyBucket(Bucket inner)
            : base(inner)
        {

        }

        public async override ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            var r = await base.ReadAsync(requested);

            if (!r.IsEof && r.Length == 0)
                throw new InvalidOperationException($"{typeof(TBucket)}.ReadAsync returns 0 length date, which is not EOF");

            return r;
        }

        protected override VerifyBucket<TBucket>? WrapDuplicate(Bucket duplicatedInner, bool reset)
        {
            return new VerifyBucket<TBucket>(duplicatedInner);
        }

        Bucket IBucketAggregation.Append(Bucket bucket)
        {
            if (bucket is not IBucketVerify)
                bucket = new VerifyBucket<Bucket>(bucket);

            return new VerifyBucket<TBucket>(new AggregateBucket(CanReset, this, bucket));
        }

        Bucket IBucketAggregation.Prepend(Bucket bucket)
        {
            if (bucket is not IBucketVerify)
                bucket = new VerifyBucket<Bucket>(bucket);

            return new VerifyBucket<TBucket>(new AggregateBucket(CanReset, bucket, this));
        }
    }
}
