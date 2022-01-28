﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Amp.Buckets.Specialized
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


        public class PollData : IDisposable
        {
            public Bucket Bucket { get; }
            public BucketBytes Data { get; }
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

            public void Dispose()
            {
                if (AlreadyRead > 0)
                    throw new BucketException($"{AlreadyRead} polled bytes were not consumed");
            }
        }

        public static async ValueTask<PollData> PollAsync(this Bucket self, int minRequested = 1)
        {
            var data = await self.PeekAsync();

            if (data.Length >= minRequested)
                return new PollData(self, data, 0); // Nice peek, move along

            data = await self.ReadAsync(minRequested);

            if (data.IsEmpty)
                return new PollData(self, BucketBytes.Eof, 0); // Nothing to optimize

            byte byte0 = data[0];
            byte[]? dataBytes = (data.Length > 0) ? data.ToArray() : null;
            int alreadyRead = data.Length;

            // Now the special trick, we might just have triggered a much longer read and in
            // that case we want to provide more data

            data = await self.PeekAsync();

            var (arr, offset) = data;

            if (arr is not null && offset > alreadyRead)
            {
                if ((alreadyRead == 1 && arr[offset-1] == byte0)
                    || arr.Skip(offset - alreadyRead).Take(alreadyRead).SequenceEqual(dataBytes!))
                {
                    // The very lucky, but common case. The peek buffer starts with what we read

                    return new PollData(self, new BucketBytes(arr, offset - alreadyRead, data.Length + alreadyRead), alreadyRead);
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

                for(int i = alreadyRead; i < result.Length; i++)
                {
                    result[i] = data[i - alreadyRead];
                }
                dataBytes = result;
            }
            else if (dataBytes == null)
                dataBytes = new[] { byte0 };

            return new PollData(self, dataBytes, alreadyRead);
        }
    }
}
