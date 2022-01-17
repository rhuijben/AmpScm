using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Amp.Buckets
{
    public abstract class Bucket : IAsyncDisposable, IDisposable
    {
        public static readonly Bucket Empty = new EmptyBucket();
        internal static readonly ValueTask<BucketBytes> EofTask = new ValueTask<BucketBytes>(BucketBytes.Eof);
        internal static readonly ValueTask<BucketBytes> EmptyTask = new ValueTask<BucketBytes>(BucketBytes.Empty);

        protected Bucket()
        {

        }

        public abstract ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue);

        public abstract ValueTask<BucketBytes> PeekAsync(bool noPoll = false);

        public virtual ValueTask<int> ReadSkipAsync(int requested)
        {
            return SkipByReading(requested);
        }

        internal async ValueTask<int> SkipByReading(int requested)
        {
            int skipped = 0;
            while (requested > 0)
            {
                var v = await ReadAsync(requested);
                if (v.Length == 0)
                    break;

                requested -= v.Length;
                skipped += v.Length;                
            }
            return skipped;
        }

        public virtual ValueTask<long?> ReadRemainingBytesAsync()
        {
            return new ValueTask<long?>((long?)null);
        }

        public virtual long? Position => null;

        public virtual ValueTask<Bucket> DuplicateAsync(bool reset)
        {
            if (reset && !CanReset)
                throw new InvalidOperationException();

            throw new NotSupportedException();
        }

        public virtual bool CanReset => false;

        public virtual ValueTask ResetAsync()
        {
            if (!CanReset)
                throw new InvalidOperationException();

            return new ValueTask();
        }

        sealed class EmptyBucket : Bucket
        {
            public override ValueTask<BucketBytes> PeekAsync(bool noPoll)
            {
                return EmptyTask;
            }

            public override ValueTask<BucketBytes> ReadAsync(int requested = -1)
            {
                return EofTask;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore();
            Dispose(false);
            GC.SuppressFinalize(this);
        }

        protected virtual ValueTask DisposeAsyncCore()
        {
            return new ValueTask();
        }
    }
}
