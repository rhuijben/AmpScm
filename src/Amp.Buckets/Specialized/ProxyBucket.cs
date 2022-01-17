using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Amp.Buckets.Specialized
{
    public class ProxyBucket : Bucket
    {
        protected Bucket Inner { get; }
        bool _noDispose;

        public ProxyBucket(Bucket inner)
        {
            Inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        internal ProxyBucket(Bucket inner, bool noDispose)
            : this(inner)
        {
            _noDispose = noDispose;
        }

        public override ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            return Inner.ReadAsync(requested);
        }

        public override bool CanReset => Inner.CanReset;

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
            return SkipByReading(requested);
        }

        public override ValueTask ResetAsync()
        {
            return Inner.ResetAsync();
        }

        public override ValueTask<Bucket> DuplicateAsync(bool reset)
        {
            return Inner.DuplicateAsync(reset); // Yes the duplicate *is* owned, otherwise it wouldn't have an owner
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing && !_noDispose)
                Inner.Dispose();
        }

        protected async override ValueTask DisposeAsyncCore()
        {
            if (!_noDispose)
                await Inner.DisposeAsync();

            await base.DisposeAsyncCore();
        }
    }
}
