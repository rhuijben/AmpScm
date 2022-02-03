using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Specialized
{
    public static class SpecializedBucketExtensions
    {
        public static CreateHashBucket SHA1(this Bucket self, Action<byte[]> created)
        {
            return new CreateHashBucket(self, System.Security.Cryptography.SHA1.Create(), created);
        }

        public static CreateHashBucket SHA256(this Bucket self, Action<byte[]> created)
        {
            return new CreateHashBucket(self, System.Security.Cryptography.SHA256.Create(), created);
        }

        public static CreateHashBucket MD5(this Bucket self, Action<byte[]> created)
        {
            return new CreateHashBucket(self, System.Security.Cryptography.MD5.Create(), created);
        }

        public static CreateHashBucket Crc32(this Bucket self, Action<int> created)
        {
            return new CreateHashBucket(self, CreateHashBucket.Crc32.Create(), (v) => created(BitConverter.ToInt32(v.ReverseIfLittleEndian().ToArray(), 0)));
        }

        public static IEnumerable<T> ReverseIfLittleEndian<T>(this IEnumerable<T> self)
        {
            if (BitConverter.IsLittleEndian)
                return self.Reverse();
            else
                return self;
        }

        public async static ValueTask<BucketBytes> ReadFullAsync(this Bucket self, int requested)
        {
            IEnumerable<byte>? result = null;

            while (true)
            {
                var bb = await self.ReadAsync(requested);

                if (bb.IsEof)
                    return (result != null) ? result.ToArray() : bb;

                requested -= bb.Length;

                if (result == null)
                    result = bb.ToArray();
                else
                    result = result.Concat(bb.ToArray());

                if (requested == 0)
                {
                    return (result as byte[]) ?? result.ToArray();
                }
            }
        }

        public async static ValueTask<(BucketBytes, BucketEol)> ReadUntilEolFullAsync(this Bucket self, BucketEol acceptableEols, BucketEolState? eolState, int requested = int.MaxValue)
        {
            IEnumerable<byte>? result = null;

            if (eolState?._kept.HasValue ?? false)
            {
                var kept = eolState._kept!.Value;
                eolState._kept = null;

                switch (kept)
                {
                    case (byte)'\r' when (0 != (acceptableEols & BucketEol.CR)):
                        return (new BucketBytes(new[] { kept }, 0, 1), BucketEol.CR);
                    case (byte)'\0' when (0 != (acceptableEols & BucketEol.Zero)):
                        return (new BucketBytes(new[] { kept }, 0, 1), BucketEol.Zero);
                    default:
                        result = new[] { kept };
                        break;
                }
            }
            else if (0 != (acceptableEols & BucketEol.CRLF) && eolState == null)
            {
                throw new ArgumentNullException(nameof(eolState));
            }
            while (true)
            {
                BucketBytes bb;
                BucketEol eol;

                (bb, eol) = await self.ReadUntilEolAsync(acceptableEols);

                if (bb.IsEof)
                    return ((result != null) ? result.ToArray() : bb, eol);
                else if (bb.IsEmpty)
                    throw new InvalidOperationException();
                else if (result == null && eol != BucketEol.None && eol != BucketEol.CRSplit)
                    return (bb, eol);

                requested -= bb.Length;

                if (result == null)
                    result = bb.ToArray();
                else
                    result = result.Concat(bb.ToArray());

                if (requested == 0)
                {
                    return ((result as byte[]) ?? result.ToArray(), eol);
                }
                else if (eol == BucketEol.CRSplit)
                {
                    // Bad case. We may have a \r that might be a \n

                    var poll = await self.PollAsync(1);

                    if (!poll.Data.IsEmpty && bb[0] == '\n')
                    {
                        // Phew, we were lucky. We got a \r\n
                        result = result.Concat(new byte[] { bb[0] }).ToArray();

                        await poll.Consume(1);

                        return (result.ToArray(), BucketEol.CRLF);
                    }
                    else if (!poll.Data.IsEmpty)
                    {
                        // We got something else
                        if (0 != (acceptableEols & BucketEol.CR))
                        {
                            // Keep the next byte for the next read :(
                            eolState!._kept = bb[0];
                            await poll.Consume(1);
                            return (result.ToArray(), BucketEol.CR);
                        }
                        else
                        {
                            await poll.Consume(1);
                            result = result.Concat(new byte[] { bb[0] }).ToArray();
                            continue;
                        }
                    }
                    else
                    {
                        // We are at eof, so no issues with future reads
                        eol = (0 != (acceptableEols & BucketEol.CR) ? BucketEol.CR : BucketEol.None);

                        return (result.ToArray(), eol);
                    }
                }
                else if (eol == BucketEol.None)
                    continue;
                else
                {
                    return ((result as byte[]) ?? result.ToArray(), eol);
                }
            }
        }

        public async static ValueTask<BucketBytes> ReadUntilAsync(this Bucket self, byte b)
        {
            IEnumerable<byte>? result = null;

            while (true)
            {
                using var poll = await self.PollAsync();

                if (poll.Data.IsEof)
                    return (result != null) ? new BucketBytes(result.ToArray()) : poll.Data;

                for (int i = 0; i < poll.Data.Length; i++)
                {
                    if (poll[i] == b)
                    {
                        BucketBytes r;
                        if (result == null)
                            r = poll.Data.Slice(0, i + 1).ToArray(); // Make copy, as data is transient
                        else
                            r = result.Concat(poll.Data.Slice(0, i + 1).ToArray()).ToArray();

                        await poll.Consume(i + 1);
                        return r;
                    }
                }

                var extra = poll.Data.ToArray();
                if (result == null)
                    result = extra;
                else
                    result = result.Concat(extra);

                await poll.Consume(poll.Length);
            }
        }

        public static int CharCount(this BucketEol eol)
            => eol switch
            {
                BucketEol.CRLF => 2,
                BucketEol.None => 0,
                _ => 1,
            };        
    }
}
