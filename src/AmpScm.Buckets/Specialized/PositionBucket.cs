using System.Threading.Tasks;

namespace AmpScm.Buckets.Specialized
{
    internal class PositionBucket : ProxyBucket<PositionBucket>.WithPoll
    {
        long _position;

        public PositionBucket(Bucket inner)
            : base(inner)
        {
        }

        public async override ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            var v = await Inner.ReadAsync(requested).ConfigureAwait(false);

            _position += v.Length;
            return v;
        }

        public async override ValueTask<int> ReadSkipAsync(int requested)
        {
            var v = await Inner.ReadSkipAsync(requested).ConfigureAwait(false);

            _position += v;
            return v;
        }

        public async override ValueTask ResetAsync()
        {
            await base.ResetAsync().ConfigureAwait(false);
            _position = 0;
        }

        protected override PositionBucket? WrapDuplicate(Bucket duplicatedInner, bool reset)
        {
            var p = NewPositionBucket(duplicatedInner);
            if (!reset)
                p._position = _position;

            return p;
        }

        protected virtual PositionBucket NewPositionBucket(Bucket duplicatedInner)
        {
            return new PositionBucket(duplicatedInner);
        }

        protected void SetPosition(long position)
        {
            _position = position;
        }

        public override long? Position => _position;
    }
}
