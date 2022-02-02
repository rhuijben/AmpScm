using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Specialized
{
    public class PollData : IDisposable
    {
        public Bucket Bucket { get; }
        public BucketBytes Data { get; private set; }
        public int AlreadyRead { get; private set; }

        public long? Position => Bucket.Position - AlreadyRead;

        public int Length => Data.Length;

        public PollData(Bucket bucket, BucketBytes data, int alreadyRead)
        {
            Bucket = bucket;
            Data = data;
            AlreadyRead = alreadyRead;
        }

        public async ValueTask Consume(int readBytes)
        {
            if (readBytes < AlreadyRead)
                throw new InvalidOperationException();
            if (AlreadyRead > 0)
            {
                int consume = Math.Min(readBytes, AlreadyRead);
                AlreadyRead -= consume;
                readBytes -= consume;
            }

            while (readBytes > 0)
            {
                var r = await Bucket.ReadAsync(readBytes);

                if (r.IsEmpty)
                    throw new BucketException("EOF during poll consume");

                readBytes -= r.Length;
            }
        }

        public async ValueTask<BucketBytes> ReadAsync(int readBytes)
        {
            try
            {
                if (AlreadyRead == 0)
                {
                    return await Bucket.ReadAsync(readBytes);
                }
                else if (readBytes <= AlreadyRead)
                {
                    if (readBytes < AlreadyRead)
                        throw new InvalidOperationException();

                    AlreadyRead = 0;
                    return Data.Slice(0, readBytes);
                }
                else if (readBytes > Data.Length)
                {
                    byte[] returnData;

                    if (readBytes < Data.Length)
                        returnData = Data.Slice(0, readBytes).ToArray();
                    else
                        returnData = Data.ToArray();

                    int consume = readBytes - AlreadyRead;
                    int copy = AlreadyRead;
                    AlreadyRead = 0; // No errors in Dispose please

                    var bb = await Bucket.ReadAsync(consume);

                    if (bb.IsEof)
                        return new BucketBytes(returnData, 0, copy);

                    if (copy + bb.Length <= returnData.Length)
                        return new BucketBytes(returnData, 0, copy + bb.Length); // Data already available from peek buffer

                    // We got new and old data, but how can we return that?
                    var (arr, offset) = bb;

                    if (arr is not null && offset >= copy)
                    {
                        bool equal = true;
                        for (int i = 0; i < copy; i++)
                        {
                            if (arr[offset - copy + 1] != returnData[i])
                            {
                                equal = false;
                                break;
                            }
                        }

                        if (equal)
                        {
                            return new BucketBytes(arr, offset - copy, bb.Length + copy);
                        }
                    }

                    byte[] ret = new byte[bb.Length + copy];

                    Array.Copy(returnData, 0, ret, 0, copy);
                    bb.Span.CopyTo(new Span<byte>(ret, copy, bb.Length));

                    return ret;
                }
                else
                {
                    int consume = readBytes - AlreadyRead;

                    BucketBytes slicedDataCopy = Data.Slice(0, Math.Min(readBytes, Data.Length)).ToArray();

                    var bb = await Bucket.ReadAsync(consume);

                    AlreadyRead = Math.Max(0, AlreadyRead - bb.Length);

                    if (bb.Length == consume)
                        return slicedDataCopy;
                    else if (bb.Length < consume)
                        return slicedDataCopy.Slice(0, slicedDataCopy.Length - (consume - bb.Length));
                    else
                        throw new InvalidOperationException();
                }
            }
            finally
            {
                Data = BucketBytes.Empty;
            }
        }

        public void Dispose()
        {
            if (AlreadyRead > 0)
                throw new BucketException($"{AlreadyRead} polled bytes were not consumed");
        }

        public byte this[int index] => Data[index];

        public bool IsEmpty => Data.IsEmpty;
        public bool IsEof => Data.IsEof;
    }
}
