using Amp.Buckets.Specialized;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Amp.Buckets
{
    public static class BucketExtensions
    {
        public static Bucket Append(this Bucket self, Bucket appendee)
        {
            if (self is IBucketAggregation col)
                return col.Append(appendee);
            else
            {
                return new AggregateBucket(self, appendee);
            }
        }

        public static Bucket Prepend(this Bucket self, Bucket newFirst)
        {
            if (self is IBucketAggregation col)
                return col.Prepend(newFirst);
            else
            {
                return new AggregateBucket(newFirst, self);
            }
        }

        public static Bucket WithPosition(this Bucket self)
        {
            if (self is null)
                throw new ArgumentNullException(nameof(self));

            if (self.Position != null)
                return self;

            return new PositionBucket(self);
        }

        public static Bucket Take(this Bucket self, long limit)
        {
            if (self is null)
                throw new ArgumentNullException(nameof(self));
            else if (limit < 0)
                throw new ArgumentOutOfRangeException(nameof(limit));

            if (self is TakeBucket lb)
                return lb.Take(limit);
            else
                return new TakeBucket(self, limit);
        }

        public static Bucket Skip(this Bucket self, long firstPosition)
        {
            if (self is null)
                throw new ArgumentNullException(nameof(self));
            else if (firstPosition < 0)
                throw new ArgumentOutOfRangeException(nameof(firstPosition));

            if (self is SkipBucket sb)
                return sb.Skip(firstPosition);
            else
                return new SkipBucket(self, firstPosition);
        }

        public static Bucket VerifyBehavior<TBucket>(this TBucket toVerify)
            where TBucket : Bucket
        {
            return new VerifyBucket<TBucket>(toVerify);
        }

        public static Bucket AsBucket(this byte[] bytes)
        {
            if ((bytes?.Length ?? 0) == 0)
                return Bucket.Empty;

            return new MemoryBucket(bytes!);
        }

        public static Bucket AsBucket(this byte[][] bytes)
        {
            return bytes.Select(x => x.AsBucket()).AsBucket();
        }

        public static Bucket AsBucket(this byte[][] bytes, bool keepOpen)
        {
            return bytes.Select(x => x.AsBucket()).AsBucket(keepOpen);
        }

        public static Bucket AsBucket(this ReadOnlyMemory<byte> memory)
        {
            return new MemoryBucket(memory);
        }

        public static Bucket AsBucket(this IEnumerable<Bucket> buckets)
        {
            if (!buckets.Any())
                return Bucket.Empty;

            return new AggregateBucket(buckets.ToArray());
        }

        public static Bucket AsBucket(this IEnumerable<Bucket> buckets, bool keepOpen)
        {
            if (!buckets.Any())
                return Bucket.Empty;

            return new AggregateBucket(keepOpen, buckets.ToArray());
        }

        public static Bucket Decompress(this Bucket self, BucketCompressionAlgorithm algorithm)
        {
            switch(algorithm)
            {
                case BucketCompressionAlgorithm.ZLib:
#if NET6_0_OR_GREATER
                    return new CompressionBucket(self, (inner) => new ZLibStream(inner, CompressionMode.Decompress));
#else
                    return new CompressionBucket(self.Skip(2), (inner) => new DeflateStream(inner, CompressionMode.Decompress));
#endif
                case BucketCompressionAlgorithm.Deflate:
                    return new CompressionBucket(self, (inner) => new DeflateStream(inner, CompressionMode.Decompress));
#if NETCOREAPP
                case BucketCompressionAlgorithm.Brotli:
                    return new CompressionBucket(self, (inner) => new BrotliStream(inner, CompressionMode.Decompress));
#endif
                default:
                    throw new ArgumentOutOfRangeException(nameof(algorithm));
            }
        }

        public static async ValueTask<byte[]> ToArrayAsync(this Bucket self)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                BucketBytes bb;
                while (!(bb = await self.ReadAsync()).IsEof)
                {
                    ms.Write(bb.ToArray(), 0, bb.Length);
                }

                return ms.ToArray();
            }
        }

        public static byte[] ToArray(this Bucket self)
        {
            return ToArrayAsync(self).ConfigureAwait(true).GetAwaiter().GetResult();
        }

#if NETFRAMEWORK
        internal static string GetString(this System.Text.Encoding encoding, ReadOnlySpan<byte> bytes)
        {
            return encoding.GetString(bytes.ToArray());
        }
#endif
    }
}
