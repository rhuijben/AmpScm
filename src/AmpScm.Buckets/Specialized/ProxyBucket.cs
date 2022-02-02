using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;

namespace AmpScm.Buckets.Specialized
{
    public abstract class ProxyBucket<TBucket> : WrappingBucket, IBucketNoClose
        where TBucket : Bucket
    {
        protected ProxyBucket(Bucket inner)
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

        public override ValueTask<BucketBytes> PeekAsync()
        {
            return Inner.PeekAsync();
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

        public override ValueTask<(BucketBytes, BucketEol)> ReadUntilEolAsync(BucketEol acceptableEols, int requested = int.MaxValue)
        {
            return Inner.ReadUntilEolAsync(acceptableEols, requested);
        }

        public override ValueTask<TReadBucket?> ReadBucket<TReadBucket>()
            where TReadBucket : class
        {
            return Inner.ReadBucket<TReadBucket>();
        }

        protected virtual TBucket? WrapDuplicate(Bucket duplicatedInner, bool reset)
        {
            return null;
        }

        public new virtual Bucket NoClose()
        {
            base.NoClose();
            return this;
        }
    }

    public class ProxyBucket : ProxyBucket<ProxyBucket>
    {
        string? _name;
        public ProxyBucket(Bucket inner) : base(inner)
        {

        }

        public override string Name => _name ?? (_name = (GetType() == typeof(ProxyBucket) ? "Proxy" : base.Name) + ">" + Inner.Name);


        internal ProxyBucket(Bucket inner, bool noDispose) : base(inner, noDispose)
        {
        }
    }
}
