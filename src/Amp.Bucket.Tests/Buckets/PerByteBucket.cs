using Amp.Buckets;
using Amp.Buckets.Specialized;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Amp.BucketTests.Buckets
{
    public sealed class PerByteBucket : ProxyBucket<PerByteBucket>
    {
        public PerByteBucket(Bucket inner) : base(inner)
        {
        }

        public override async ValueTask<BucketBytes> PeekAsync()
        {
            var b = await base.PeekAsync();

            if (b.Length > 1)
                return b.Slice(0, 1);
            else
                return b;
        }

        public override ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            return base.ReadAsync(1);
        }

        public override ValueTask<int> ReadSkipAsync(int requested)
        {
            return base.ReadSkipAsync(1);
        }
    }
}
