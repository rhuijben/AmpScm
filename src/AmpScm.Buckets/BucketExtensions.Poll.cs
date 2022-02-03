using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Buckets
{
    partial class BucketExtensions
    {
        public static async ValueTask<BucketPollBytes> PollAsync(this Bucket self, int minRequested = 1)
        {
            BucketBytes data;
            if (self is IBucketPoll bucketPoll)
            {
                data = await bucketPoll.PollAsync(minRequested);

                if (!data.IsEmpty || data.IsEof)
                    return new BucketPollBytes(self, data, 0);
            }
            else
                data = await self.PeekAsync();

            if (data.Length >= minRequested)
                return new BucketPollBytes(self, data, 0); // Nice peek, move along

            data = await self.ReadAsync(minRequested);

            if (data.IsEmpty)
                return new BucketPollBytes(self, BucketBytes.Eof, 0); // Nothing to optimize

            byte byte0 = data[0];
            byte[]? dataBytes = (data.Length > 0) ? data.ToArray() : null;
            int alreadyRead = data.Length;

            // Now the special trick, we might just have triggered a much longer read and in
            // that case we want to provide more data

            data = await self.PeekAsync();

            var (arr, offset) = data;

            if (arr is not null && offset > alreadyRead)
            {
                if ((alreadyRead == 1 && arr[offset - 1] == byte0)
                    || arr.Skip(offset - alreadyRead).Take(alreadyRead).SequenceEqual(dataBytes!))
                {
                    // The very lucky, but common case. The peek buffer starts with what we read

                    return new BucketPollBytes(self, new BucketBytes(arr, offset - alreadyRead, data.Length + alreadyRead), alreadyRead);
                }
            }

            if (data.Length > 0)
            {
                // We have original data and peeked data. Let's copy some data to help our caller
                byte[] result = new byte[alreadyRead + Math.Min(data.Length, 256)];

                if (alreadyRead == 1)
                    result[0] = byte0;
                else
                    Array.Copy(dataBytes!, result, alreadyRead);

                for (int i = alreadyRead; i < result.Length; i++)
                {
                    result[i] = data[i - alreadyRead];
                }
                dataBytes = result;
            }
            else if (dataBytes == null)
                dataBytes = new[] { byte0 };

            return new BucketPollBytes(self, dataBytes, alreadyRead);
        }
    }
}
