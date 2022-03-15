using System;
using System.Diagnostics;
using System.Threading.Tasks;

[assembly: CLSCompliant(true)]

namespace AmpScm.Buckets
{
    [DebuggerDisplay("{Name}: Position={Position}")]
    public abstract partial class Bucket : IAsyncDisposable, IDisposable
    {
        public static readonly Bucket Empty = new EmptyBucket();
        protected internal static readonly ValueTask<BucketBytes> EofTask = new ValueTask<BucketBytes>(BucketBytes.Eof);
        protected internal static readonly ValueTask<BucketBytes> EmptyTask = new ValueTask<BucketBytes>(BucketBytes.Empty);

        protected Bucket()
        {

        }

        public virtual string Name
        {
            get => BaseName;
        }

        internal string BaseName
        {
            get
            {
                string name = GetType().Name;

                if (name.Length > 6 && name.EndsWith("Bucket", StringComparison.OrdinalIgnoreCase))
                    return name.Substring(0, name.Length - 6);
                else
                    return name;
            }
        }

        public abstract ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue);

        public virtual BucketBytes Peek()
        {
            return BucketBytes.Empty;
        }

        public virtual ValueTask<int> ReadSkipAsync(int requested)
        {
            return SkipByReading(requested);
        }

        internal async ValueTask<int> SkipByReading(int requested)
        {
            int skipped = 0;
            while (requested > 0)
            {
                var v = await ReadAsync(requested).ConfigureAwait(false);
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
                throw new InvalidOperationException($"Reset not supported on {Name} bucket");

            throw new NotSupportedException($"DuplicateAsync not implemented on {Name} bucket");
        }

        public virtual bool CanReset => false;

        public virtual ValueTask ResetAsync()
        {
            if (!CanReset)
                throw new InvalidOperationException($"Reset not supported on {Name} bucket");

            return default;
        }

        public virtual ValueTask<TBucket?> ReadBucket<TBucket>()
            where TBucket : Bucket
        {
            return default;
        }

        sealed class EmptyBucket : Bucket
        {
            public override ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
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
            await DisposeAsyncCore().ConfigureAwait(false);
            Dispose(false);
            GC.SuppressFinalize(this);
        }

        protected virtual ValueTask DisposeAsyncCore()
        {
            return default;
        }

        public override string ToString()
        {
            return Name;
        }

#pragma warning disable CA2225 // Operator overloads have named alternates
        public static Bucket operator +(Bucket first, Bucket second)
#pragma warning restore CA2225 // Operator overloads have named alternates
        {
            if (first is null)
                return second;
            else if (second is null)
                return first;
            else
                return first.Append(second);
        }
    }
}
