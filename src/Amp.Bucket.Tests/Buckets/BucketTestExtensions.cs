using Amp.Buckets;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Amp.BucketTests.Buckets
{
    public static class BucketTestExtensions
    {
        public static Bucket PerByte(this Bucket self)
        {
            return new PerByteBucket(self);
        }

        public static async ValueTask BucketsEqual(this Assert self, Bucket left, Bucket right)
        {
            long p = 0;

            while(true)
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
