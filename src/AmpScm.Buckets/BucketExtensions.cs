using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using AmpScm.Buckets.Specialized;
using AmpScm.Buckets.Interfaces;
using System.ComponentModel;

namespace AmpScm.Buckets
{
    public static partial class BucketExtensions
    {
        public static Bucket Append(this Bucket self, Bucket newLast)
        {
            if (self is IBucketAggregation col)
                return col.Append(newLast);
            else
            {
                return new AggregateBucket(self, newLast);
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

        public static Bucket WithPosition(this Bucket self, bool alwaysWrap = false)
        {
            if (self is null)
                throw new ArgumentNullException(nameof(self));

            if (!alwaysWrap && self.Position != null)
                return self;

            return new PositionBucket(self);
        }

        public static Bucket Take(this Bucket self, long limit, bool alwaysWrap = false)
        {
            if (self is null)
                throw new ArgumentNullException(nameof(self));
            else if (limit < 0)
                throw new ArgumentOutOfRangeException(nameof(limit));

            if (!alwaysWrap && self is IBucketTake take)
                return take.Take(limit);
            else
                return new TakeBucket(self, limit);
        }

        public static Bucket Skip(this Bucket self, long firstPosition, bool alwaysWrap = false)
        {
            if (self is null)
                throw new ArgumentNullException(nameof(self));
            else if (firstPosition < 0)
                throw new ArgumentOutOfRangeException(nameof(firstPosition));

            if (!alwaysWrap && self is IBucketSkip sb)
                return sb.Skip(firstPosition);
            else
                return new SkipBucket(self, firstPosition);
        }

        public static Bucket NoClose(this Bucket bucket, bool alwaysWrap = false)
        {
            if (!alwaysWrap && bucket is IBucketNoClose nc)
                return nc.NoClose();
            else
                return new NoCloseBucket(bucket);
        }

        public static Bucket SeekOnReset(this Bucket bucket)
        {
            return SkipBucket.SeekOnReset(bucket);
        }

        public static Bucket Wrap(this Bucket self)
        {
            return new ProxyBucket.Sealed(self);
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

        [CLSCompliant(false)]
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
            switch (algorithm)
            {
                case BucketCompressionAlgorithm.ZLib:
                    return new ZLibBucket(self);
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
            if (self is null)
                throw new ArgumentNullException(nameof(self));

            using (MemoryStream ms = new MemoryStream())
            {
                BucketBytes bb;
                while (!(bb = await self.ReadAsync().ConfigureAwait(false)).IsEof)
                {
                    await ms.WriteAsync(bb.ToArray(), 0, bb.Length).ConfigureAwait(false);
                }

                return ms.ToArray();
            }
        }

        public static byte[] ToArray(this Bucket self)
        {
#pragma warning disable CA2012 // Use ValueTasks correctly
            return ToArrayAsync(self).ConfigureAwait(true).GetAwaiter().GetResult();
#pragma warning restore CA2012 // Use ValueTasks correctly
        }

        public static Stream AsStream(this Bucket self)
        {
            return new Wrappers.BucketStream(self);
        }

        public static TextReader AsReader(this Bucket self)
        {
            return new Wrappers.BucketReader(self, null);
        }

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public static byte[] ReverseInPlace(this byte[] array)
        {
            if (array is null)
                throw new ArgumentNullException(nameof(array));

            // Use Array.Reverse() ?
            for (int first = array.GetLowerBound(0), last = array.GetUpperBound(0);
                first < last;
                first++, last--)
            {
                var tmp = array[first];
                array[first] = array[last];
                array[last] = tmp;
            }
            return array;
        }

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public static byte[] ReverseInPlaceIfLittleEndian(this byte[] array)
        {
            return BitConverter.IsLittleEndian ? ReverseInPlace(array) : array;
        }

#if NETFRAMEWORK
        internal static string GetString(this System.Text.Encoding encoding, ReadOnlySpan<byte> bytes)
        {
            return encoding.GetString(bytes.ToArray());
        }
#endif
    }
}
