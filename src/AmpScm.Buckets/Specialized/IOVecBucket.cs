using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;

namespace AmpScm.Buckets.Specialized
{
    sealed class IovecBucket : MemoryBucket, IBucketAggregation
    {
        public IovecBucket(byte[] data) : base(data)
        {
        }

        public IovecBucket(byte[] data, int start, int length) : base(data, start, length)
        {
        }

        public IovecBucket(ReadOnlyMemory<byte> memory)
            : base(memory)
        {

        }

        Bucket IBucketAggregation.Append(Bucket bucket)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));
            else if (bucket is MemoryBucket mb && Offset == 0 && mb.Position == 0 && Data.Length + mb.Data.Length <= 2048)
            {
                byte[] together = new byte[Data.Length + mb.Data.Length];
            
                var tg = together.AsSpan();
                Data.Span.CopyTo(tg);
                mb.Data.Span.CopyTo(tg.Slice(Data.Length));
            
                return new IovecBucket(together);
            }
            else
                return new AggregateBucket(this, bucket);
        }

        Bucket IBucketAggregation.Prepend(Bucket bucket)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));
            else if (bucket is MemoryBucket mb)
            {
                byte[] together = new byte[mb.Data.Length + Data.Length];
            
                var tg = together.AsSpan();
                mb.Data.Span.CopyTo(tg);
                Data.Span.CopyTo(tg.Slice(mb.Data.Length));
            
                return new IovecBucket(together);
            }
            else
                return new AggregateBucket(bucket, this);
        }
    }
}
