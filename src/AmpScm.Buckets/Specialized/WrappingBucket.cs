using System;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Specialized
{
    public abstract class WrappingBucket : Bucket
    {
        protected Bucket Inner { get; }
        internal protected bool DontDisposeInner { get; internal set; }

        public WrappingBucket(Bucket inner)
        {
            Inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        protected WrappingBucket(Bucket inner, bool noDispose)
            : this(inner)
        {
            DontDisposeInner = noDispose;
        }

        public override string Name => base.Name + ">" + Inner.Name;

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing && !DontDisposeInner)
                Inner.Dispose();
        }

        protected async override ValueTask DisposeAsyncCore()
        {
            if (!DontDisposeInner)
                await Inner.DisposeAsync();

            await base.DisposeAsyncCore();
        }

        protected void NoClose()
        {
            DontDisposeInner = true;
        }
    }
}
