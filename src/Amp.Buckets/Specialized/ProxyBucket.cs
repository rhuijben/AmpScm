using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Amp.Buckets.Specialized
{
    
    public abstract class ProxyBucket<TBucket> : WrappingBucket
        where TBucket : Bucket
    {
        public ProxyBucket(Bucket inner)
            : base(inner)
        {
        }

        internal ProxyBucket(Bucket inner, bool noDispose)
            : base(inner, noDispose)
        {
        }

        public override ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            return Inner.ReadAsync(requested);
        }

        public override bool CanReset => Inner.CanReset;

        public override string Name => base.Name + "/" + Inner.Name;

        public override ValueTask<BucketBytes> PeekAsync(bool noPoll = false)
        {
            return Inner.PeekAsync(noPoll);
        }

        public override ValueTask<long?> ReadRemainingBytesAsync()
        {
            return Inner.ReadRemainingBytesAsync();
        }

        public override long? Position => Inner.Position;

        public override ValueTask<int> ReadSkipAsync(int requested)
        {
            return Inner.ReadSkipAsync(requested);
        }

        public override ValueTask ResetAsync()
        {
            return Inner.ResetAsync();
        }

        public override async ValueTask<Bucket> DuplicateAsync(bool reset)
        {
            var r = await Inner.DuplicateAsync(reset);
            return WrapDuplicate(r, reset) ?? r;
        }

        protected virtual TBucket? WrapDuplicate(Bucket duplicatedInner, bool reset)
        {
            return null;
        }        
    }

    public class ProxyBucket : ProxyBucket<ProxyBucket>
    {
        public ProxyBucket(Bucket inner) : base(inner)
        {

        }

        internal ProxyBucket(Bucket inner, bool noDispose) : base(inner, noDispose)
        {
        }
    }
}
