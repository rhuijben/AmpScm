using Amp.Buckets;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NUnit;
using System;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Amp.Bucket.Tests
{
    [TestClass]
    public class BasicTests
    {

        [TestMethod]
        public async Task BasicReadMemory()
        {
            byte[] buffer = Enumerable.Range(0, 256).Select(x => (byte)x).ToArray();

            await using var mb = new MemoryBucket(buffer);

            BucketBytes bb;

            bb = await mb.PeekAsync();
            Assert.AreEqual(256, bb.Length);

            Assert.AreEqual(256L, await mb.ReadRemainingBytesAsync());

            bb = await mb.ReadAsync(10);
            Assert.AreEqual(10, bb.Length);

            bb = await mb.ReadAsync();
            Assert.AreEqual(246, bb.Length);

            bb = await mb.ReadAsync();
            Assert.IsTrue(bb.IsEof);
        }

        [TestMethod]
        public async Task BasicAggregate()
        {
            byte[] buffer = Enumerable.Range(0, 256).Select(x => (byte)x).ToArray();

            var mb1 = new MemoryBucket(buffer);
            var mb = mb1.Append(new MemoryBucket(buffer));

            BucketBytes bb;

            bb = await mb.PeekAsync();
            Assert.AreEqual(256, bb.Length);

            Assert.AreEqual(512L, await mb.ReadRemainingBytesAsync());

            bb = await mb.ReadAsync(10);
            Assert.AreEqual(10, bb.Length);

            bb = await mb.ReadAsync();
            Assert.AreEqual(246, bb.Length);

            bb = await mb.ReadAsync();
            Assert.AreEqual(256, bb.Length);

            bb = await mb.ReadAsync();
            Assert.IsTrue(bb.IsEof);
        }

        [TestMethod]
        public async Task BasicHash()
        {
            var strings = new[] { "This is string1\n", "This is string2\n", "This is string 3\n" };
            byte[][] buffers = strings.Select(x => System.Text.Encoding.UTF8.GetBytes(x)).ToArray();

            var b = new AggregateBucket(true, buffers.Select(x => new MemoryBucket(x)).ToArray());            

            var r = await b.ReadSkipAsync(1024);
            Assert.AreEqual(49L, r);

            await b.ResetAsync();

            var c = new Amp.Buckets.Specialized.CreateHashBucket(b, MD5.Create());

            r = await c.ReadSkipAsync(1024);
            Assert.AreEqual(49L, r);

            Assert.IsNotNull(c.HashResult);
            Assert.AreEqual("E358B5530A87E41AF9168B4F45548AFC", FormatHash(c.HashResult));

            await b.ResetAsync();
            var c2 = new Amp.Buckets.Specialized.CreateHashBucket(b, SHA1.Create());

            r = await c2.ReadSkipAsync(1024);
            Assert.AreEqual(49L, r);

            Assert.IsNotNull(c2.HashResult);
            Assert.AreEqual("D9F7CE90FB58072D8A68F69A0CB30C133F9B08CB", FormatHash(c2.HashResult));

            await c2.ResetAsync();

            r = await c2.ReadSkipAsync(1024);
            Assert.AreEqual(49L, r);

            Assert.IsNotNull(c2.HashResult);
            Assert.AreEqual("D9F7CE90FB58072D8A68F69A0CB30C133F9B08CB", FormatHash(c2.HashResult));

#if NETCOREAPP
            {
                var rr = MD5.HashData(Encoding.ASCII.GetBytes(string.Join("", strings)));

                Assert.AreEqual("E358B5530A87E41AF9168B4F45548AFC", FormatHash(rr));

                rr = SHA1.HashData(Encoding.ASCII.GetBytes(string.Join("", strings)));

                Assert.AreEqual("D9F7CE90FB58072D8A68F69A0CB30C133F9B08CB", FormatHash(rr));
            }
#endif
        }

        private string FormatHash(byte[] hashResult)
        {
            var sb = new StringBuilder();
            foreach(var b in hashResult)
                sb.Append(b.ToString("X2"));

            return sb.ToString();
        }
    }
}