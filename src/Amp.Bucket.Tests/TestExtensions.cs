﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amp.Buckets;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Amp.BucketTests
{
    internal static class TestExtensions
    {
        public static void ReadFull(this Bucket self, byte[] array)
        {
            int pos = 0;

            while (pos < array.Length)
            {
                var r = self.ReadAsync(array.Length - pos);

                BucketBytes bb;

                if (r.IsCompleted)
                    bb = r.Result;
                else
                    bb = r.GetAwaiter().GetResult();

                bb.CopyTo(new Memory<byte>(array, pos, bb.Length));
                pos += bb.Length;
                if (bb.IsEof)
                    throw new InvalidOperationException();
            }
        }

        public static async ValueTask<byte[]> ReadToEnd(this Bucket self)
        {
            List<byte> bytes = new List<byte>();
            BucketBytes bb;
            while (!(bb = await self.ReadAsync()).IsEof)
            {
                bytes.AddRange(bb.ToArray());
            }

            return bytes.ToArray();
        }

        public static async ValueTask BucketsEqual(this Assert self, Bucket left, Bucket right)
        {
            long p = 0;

            while (true)
            {
                var l1 = await left.ReadAsync(1);
                var r1 = await right.ReadAsync(1);

                if (l1.IsEof || r1.IsEof)
                {
                    if (l1.IsEof && r1.IsEof)
                        return;

                    Assert.AreEqual(l1.IsEof, r1.IsEof, $"Expected both EOF at same time, pos={p}");
                }

                Assert.AreEqual(1, l1.Length, "Expected 1 byte left");
                Assert.AreEqual(1, r1.Length, "Expected 1 byte right");

                Assert.AreEqual(l1[0], r1[0], $"Expected bytes equal at position {p}");
                p++;

                //Console.Write($"{l1[0]:x2} ");
                //
                //if (p % 16 == 0)
                //    Console.WriteLine();
            }
        }
    }
}
