﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Git;
using AmpScm.Buckets.Specialized;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AmpScm.Tests.Buckets
{
    [TestClass]
    public class GitBitmapTests
    {
        public TestContext TestContext { get; set; } = default!;

        [TestMethod]
        public async Task ReadBitmap()
        {
            string bmpFile = FindResource("*.bitmap");

            var fb = FileBucket.OpenRead(bmpFile);


            var headers = await fb.ReadFullAsync(32); // Skip headers

            uint count = NetBitConverter.ToUInt32(headers, 8);

            Assert.AreEqual(106u, count);

            //BitArray
            var bitLengths = new int[4];
            {
                var c = await fb.DuplicateAsync(false);
                Assert.AreEqual(32, c.Position);

                for (int i = 0; i < 4; i++)
                {

                    bitLengths[i] = (int)await c.ReadNetworkUInt32Async();
                    uint u2 = await c.ReadNetworkUInt32Async();
                    List<ulong> w = new List<ulong>();

                    for (uint n = 0; n < u2; n++)
                    {
                        w.Add(await c.ReadNetworkUInt64Async());
                    }
                    await c.ReadNetworkUInt32Async();

                    TestContext.WriteLine($"EWAH  {i}: {bitLengths[i]}\t{u2}");
                    foreach (var v in w)
                    {
                        TestContext.Write($"{v:X16} ");
                    }
                    TestContext.WriteLine();
                }

                //GC.KeepAlive(u1 + u2 + u3);
            }

            for (int i = 0; i < 4; i++)
            {
                using var ewah = new GitEwahBitmapBucket(fb.NoClose(true));

                Assert.AreEqual(0L, ewah.Position);

                int expectedBytes = (int)(8 * ((bitLengths[i] + 63) / 64));

                long? p = await ewah.ReadRemainingBytesAsync();

                int peekLen = ewah.Peek().Length;
                Assert.IsTrue(peekLen > 0, "Can peek something");
                Assert.IsTrue(peekLen <= expectedBytes, "No overshoot");

                Assert.AreEqual(expectedBytes, (int)p, "ReadRemaining returned expected value");

                var bb = await ewah.ReadFullAsync(65536);

                Assert.AreEqual(expectedBytes, bb.Length, $"Read {bb.Length}, expected {bitLengths[i]} bits, what would be {(bitLengths[i] + 7) / 8} bytes, or {expectedBytes} bytes when reading longs");

                StringBuilder sb = new StringBuilder();
                for (int ii = 0; ii < bb.Length; ii++)
                {
                    sb.Append(bb[ii].ToString("x2"));
                }
                TestContext.WriteLine();
                int removeAfter = 2 * ((bitLengths[i] + 7) / 8);

                if (removeAfter < sb.Length)
                    sb.Remove(removeAfter, sb.Length - removeAfter);
                TestContext.WriteLine(sb.ToString());
            }
        }

        [TestMethod]
        public async Task Read4BitmapsXor()
        {
            string bmpFile = FindResource("*.bitmap");

            var fb = FileBucket.OpenRead(bmpFile);


            var headers = await fb.ReadFullAsync(32); // Skip headers

            uint count = NetBitConverter.ToUInt32(headers, 8);

            Assert.AreEqual(106u, count);

            List<GitEwahBitmapBucket> buckets = new List<GitEwahBitmapBucket>();
            for(int i = 0; i < 4; i++)
            {
                buckets.Add(new GitEwahBitmapBucket(await fb.DuplicateAsync(false)));

                await fb.ReadNetworkUInt32Async(); // Bitlength
                uint u2 = await fb.ReadNetworkUInt32Async(); // Compressed length

                for (uint n = 0; n < u2; n++)
                {
                    await fb.ReadNetworkUInt64Async();
                }
                await fb.ReadNetworkUInt32Async(); // Last RLW start
            }

            var allXor = new BitwiseXorBucket(new BitwiseXorBucket(buckets[0], buckets[1]), new BitwiseXorBucket(buckets[2], buckets[3]));

            int maxBits = buckets.Max(x => x.ReadBitLengthAsync().GetAwaiter().GetResult());

            Assert.AreEqual(2369, maxBits);
            var bb = await allXor.ReadFullAsync((maxBits + 7) / 8);

            for (int i = 0; i < bb.Length - 1; i++)
            {
                Assert.AreEqual((byte)0xFF, bb[i]);
            }
        }

        private string FindResource(string pattern)
        {
            string dir = Path.GetDirectoryName(typeof(GitTests).Assembly.Location)!;

            var f = Directory.GetFiles(Path.Combine(dir, "cases"), pattern).FirstOrDefault();

            return f;
        }
    }
}
