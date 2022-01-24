using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Amp.Buckets.Specialized
{
    public abstract class WrappingBucket : Bucket, IBucketNoClose
    {
        protected Bucket Inner { get; }
        bool _noDispose;

        public WrappingBucket(Bucket inner)
        {
            Inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        protected WrappingBucket(Bucket inner, bool noDispose)
            : this(inner)
        {
            _noDispose = noDispose;
        }

        public override string Name => base.Name + ">" + Inner.Name;

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

        public virtual Bucket NoClose()
        {
            _noDispose = true;
            return this;
        }
    }
}
