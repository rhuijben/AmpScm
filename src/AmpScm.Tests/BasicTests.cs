using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Specialized;
using AmpScm.BucketTests.Buckets;
using AmpScm.Git.Objects;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AmpScm.Tests
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

            bb = mb.Peek();
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

            bb = mb.Peek();
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

            var b = buffers.AsBucket(true);

            var r = await b.ReadSkipAsync(1024);
            Assert.AreEqual(49L, r);

            await b.ResetAsync();

            var c = new AmpScm.Buckets.Specialized.CreateHashBucket(b, MD5.Create());

            r = await c.ReadSkipAsync(1024);
            Assert.AreEqual(49L, r);

            Assert.IsNotNull(c.HashResult);
            Assert.AreEqual("E358B5530A87E41AF9168B4F45548AFC", FormatHash(c.HashResult));

            await b.ResetAsync();
            var c2 = new AmpScm.Buckets.Specialized.CreateHashBucket(b, SHA1.Create());

            r = await c2.ReadSkipAsync(1024);
            Assert.AreEqual(49L, r);

            Assert.IsNotNull(c2.HashResult);
            Assert.AreEqual("D9F7CE90FB58072D8A68F69A0CB30C133F9B08CB", FormatHash(c2.HashResult));

            await c2.ResetAsync();

            r = await c2.ReadSkipAsync(1024);
            Assert.AreEqual(49L, r);

            Assert.IsNotNull(c2.HashResult);
            Assert.AreEqual("D9F7CE90FB58072D8A68F69A0CB30C133F9B08CB", FormatHash(c2.HashResult));

#if NET5_0_OR_GREATER
            {
                var rr = MD5.HashData(Encoding.ASCII.GetBytes(string.Join("", strings)));

                Assert.AreEqual("E358B5530A87E41AF9168B4F45548AFC", FormatHash(rr));

                rr = SHA1.HashData(Encoding.ASCII.GetBytes(string.Join("", strings)));

                Assert.AreEqual("D9F7CE90FB58072D8A68F69A0CB30C133F9B08CB", FormatHash(rr));
            }
#endif
        }

        // Git blob of "ABCDEFGHIJKLMNOPQRSTUVWXYZ"
        static byte[] git_blob_ABCDEFGHIJKLMNOPQRSTUVWXYZ = {
            0x78, 0x01, 0x4b, 0xca, 0xc9, 0x4f, 0x52, 0x30, 0x32, 0x63, 0x70, 0x74,
            0x72, 0x76, 0x71, 0x75, 0x73, 0xf7, 0xf0, 0xf4, 0xf2, 0xf6, 0xf1, 0xf5,
            0xf3, 0x0f, 0x08, 0x0c, 0x0a, 0x0e, 0x09, 0x0d, 0x0b, 0x8f, 0x88, 0x8c,
            0x02, 0x00, 0xa8, 0xae, 0x0a, 0x07
        };

        [TestMethod]
        public async Task BasicDecompress()
        {
            using var b = git_blob_ABCDEFGHIJKLMNOPQRSTUVWXYZ.AsBucket();

            var r = b.Decompress(BucketCompressionAlgorithm.ZLib);


            byte[] chars34 = new byte[34];
            r.ReadFull(chars34);

            Assert.AreEqual("blob 26\0ABCDEFGHIJKLMNOPQRSTUVWXYZ", Encoding.ASCII.GetString(chars34));

            var v = await r.ReadAsync();

            Assert.IsTrue(v.IsEof);

            v = await b.ReadAsync();
            Assert.IsTrue(v.IsEof);

            await b.ResetAsync();
            r = b.PerByte().Decompress(BucketCompressionAlgorithm.ZLib);
            r.ReadFull(chars34);
            Assert.AreEqual("blob 26\0ABCDEFGHIJKLMNOPQRSTUVWXYZ", Encoding.ASCII.GetString(chars34));

            v = await r.ReadAsync();

            Assert.IsTrue(v.IsEof);

            v = await b.ReadAsync();
            Assert.IsTrue(v.IsEof);
        }

        [TestMethod]
        public async Task BasicDecompressTrail()
        {
            var b = git_blob_ABCDEFGHIJKLMNOPQRSTUVWXYZ.AsBucket().Append(new byte[] { 255, 100, 101 }.AsBucket());
            {
                var r = b.Decompress(BucketCompressionAlgorithm.ZLib);


                byte[] chars34 = new byte[34];
                r.ReadFull(chars34);

                Assert.AreEqual("blob 26\0ABCDEFGHIJKLMNOPQRSTUVWXYZ", Encoding.ASCII.GetString(chars34));

                var v = await r.ReadAsync();

                v = await b.ReadAsync();
                Assert.IsFalse(v.IsEof);
                Assert.AreEqual(3, v.Length);
            }

            b = git_blob_ABCDEFGHIJKLMNOPQRSTUVWXYZ.AsBucket().Append(new byte[] { 255, 100, 101 }.AsBucket()).ToArray().AsBucket();

            {
                var r = b.Decompress(BucketCompressionAlgorithm.ZLib);


                byte[] chars34 = new byte[34];
                r.ReadFull(chars34);

                Assert.AreEqual("blob 26\0ABCDEFGHIJKLMNOPQRSTUVWXYZ", Encoding.ASCII.GetString(chars34));

                var v = await r.ReadAsync();

                v = await b.ReadAsync();
                Assert.IsFalse(v.IsEof);
                Assert.AreEqual(3, v.Length);
            }

            b = git_blob_ABCDEFGHIJKLMNOPQRSTUVWXYZ.AsBucket().Append(new byte[] { 255, 100, 101 }.AsBucket()).ToArray().AsBucket().PerByte();

            {
                var r = b.Decompress(BucketCompressionAlgorithm.ZLib);


                byte[] chars34 = new byte[34];
                r.ReadFull(chars34);

                Assert.AreEqual("blob 26\0ABCDEFGHIJKLMNOPQRSTUVWXYZ", Encoding.ASCII.GetString(chars34));

                var v = await r.ReadAsync();

                v = await b.ReadAsync();
                Assert.IsFalse(v.IsEof);
                Assert.AreEqual(1, v.Length);
            }
        }

        [TestMethod]
        public async Task SkipTake()
        {
            var b = Encoding.ASCII.GetBytes("ABCDEFGHIJKLMNOPQRSTUVWXYZ").AsBucket();

            b = b.Skip(4).Take(10);

            var p = b.Peek();

            Assert.AreEqual(10, p.Length);
            Assert.AreEqual("EFGHIJKLMN", Encoding.ASCII.GetString(p.ToArray()));


            p = await b.ReadAsync(12);

            Assert.AreEqual(10, p.Length);
            Assert.AreEqual("EFGHIJKLMN", Encoding.ASCII.GetString(p.ToArray()));
        }

        [TestMethod]
        public void ReadAsStream()
        {
            var b = Encoding.ASCII.GetBytes("ABCDEFGHIJKLMNOPQRSTUVWXYZ").AsBucket();

            using var s = b.AsStream();

            Assert.AreEqual(26, s.Length);
            Assert.AreEqual(0, s.Position);

            Assert.AreEqual((int)'A', s.ReadByte());

            Assert.AreEqual(26, s.Length);
            Assert.AreEqual(1, s.Position);
        }

        [TestMethod]
        public void ReadAsReader()
        {
            var b = Encoding.ASCII.GetBytes("ABCDEFGHIJKLMNOPQRSTUVWXYZ").AsBucket();

            using var s = b.AsReader();

            var all = s.ReadToEnd();
            Assert.AreEqual("ABCDEFGHIJKLMNOPQRSTUVWXYZ", all);


            var b2 = Encoding.ASCII.GetBytes("ABCD\nEFGHI\rJKLMNOPQ\r\nRSTUV\0WXYZ").AsBucket();

            using var s2 = b2.AsReader();

            List<string> lines = new List<string>();


            while (s2.ReadLine() is string line)
            {
                lines.Add(line);
            }

            Assert.IsTrue(lines.Count >= 4);
            Assert.AreEqual("ABCD", lines[0]);
            Assert.AreEqual("EFGHI", lines[1]);
            Assert.AreEqual("JKLMNOPQ", lines[2]);
        }

        [TestMethod]
        public async Task ReadEols()
        {
            var (bb, eol) = await MakeBucket("abc\nabc").ReadUntilEolAsync(BucketEol.LF);

            Assert.AreEqual(4, bb.Length);
            Assert.AreEqual("abc\n", bb.ToASCIIString());
            Assert.AreEqual("abc", bb.ToASCIIString(eol));
            Assert.AreEqual(BucketEol.LF, eol);

            var r = MakeBucket("abc\0abc");
            (bb, eol) = await r.ReadUntilEolAsync(BucketEol.Zero);

            Assert.AreEqual(4, bb.Length);
            Assert.AreEqual("abc", bb.ToASCIIString(eol));
            Assert.AreEqual(BucketEol.Zero, eol);

            (bb, eol) = await r.ReadUntilEolAsync(BucketEol.Zero);

            Assert.AreEqual(3, bb.Length);
            Assert.AreEqual("abc", bb.ToASCIIString(eol));
            Assert.AreEqual(BucketEol.None, eol);

            (bb, eol) = await r.ReadUntilEolAsync(BucketEol.Zero);

            Assert.AreEqual(0, bb.Length);
            Assert.IsTrue(bb.IsEof);
            Assert.AreEqual(BucketEol.None, eol);

            r = MakeBucket("a", "b", "c", "\0a", "bc", "d\0a", "b", "c", "\0", "a");
            string total = "";
            while (true)
            {
                (bb, eol) = await r.ReadUntilEolAsync(BucketEol.Zero);

                if (bb.IsEof)
                    break;

                total += "|" + bb.ToASCIIString();

                if (eol != BucketEol.None)
                    total += "!";
            }

            Assert.AreEqual("abc\0abcd\0abc\0a", total.Replace("|", "").Replace("!", ""));
            Assert.AreEqual("|a|bc|\0!|a|bc|d\0!|a|bc|\0!|a", total);

            r = MakeBucket("a", "b", "c", "\0");
            total = "";
            while (true)
            {
                (bb, eol) = await r.ReadUntilEolAsync(BucketEol.Zero);

                if (bb.IsEof)
                    break;

                total += "|" + bb.ToASCIIString();

                if (eol != BucketEol.None)
                    total += "!";
            }

            Assert.AreEqual("abc\0", total.Replace("|", "").Replace("!", ""));
            Assert.AreEqual("|a|bc|\0!", total);


            r = MakeBucket("a\r\nb\rcd\r", "\nefg\rhi\r\n", "j\r", "\rk");

            total = "";
            while (true)
            {
                (bb, eol) = await r.ReadUntilEolAsync(BucketEol.AnyEol);

                if (bb.IsEof)
                    break;

                total += "|" + bb.ToASCIIString(eol) + $"[{eol}]";
            }

            Assert.AreEqual("|a[CRLF]|b[CR]|cd[CRSplit]|[LF]|efg[CR]|hi[CRLF]|j[CRSplit]|[CR]|k[None]",
                            total.Replace("\r", "/r/"));

            r = MakeBucket("a\r");
            total = "";
            while (true)
            {
                (bb, eol) = await r.ReadUntilEolAsync(BucketEol.AnyEol);

                if (bb.IsEof)
                    break;

                total += "|" + bb.ToASCIIString(eol) + $"[{eol}]";
            }

            Assert.AreEqual("|a[CRSplit]",
                            total.Replace("\r", "/r/"));


            r = MakeBucket("H", "T", "T", "P", "/", "1", ".", "1", "\r", "\n", "a");

            total = "";
            while (true)
            {
                (bb, eol) = await r.ReadUntilEolAsync(BucketEol.AnyEol);

                if (bb.IsEof)
                    break;

                total += "|" + bb.ToASCIIString(eol) + $"[{eol}]";
            }

            Assert.AreEqual("|H[None]|TT[None]|P/[None]|1.[None]|1[CRSplit]|[LF]|a[None]",
                            total.Replace("\r", "/r/"));
        }

        [TestMethod]
        public async Task ReadEolsFull()
        {
            var (bb, eol) = await MakeBucket("abc\nabc").ReadUntilEolFullAsync(BucketEol.LF, new BucketEolState());

            Assert.AreEqual(4, bb.Length);
            Assert.AreEqual("abc\n", bb.ToASCIIString());
            Assert.AreEqual("abc", bb.ToASCIIString(eol));
            Assert.AreEqual(BucketEol.LF, eol);

            (bb, eol) = await MakeBucket("abc\0abc").ReadUntilEolFullAsync(BucketEol.Zero, new BucketEolState());

            Assert.AreEqual(4, bb.Length);
            Assert.AreEqual("abc", bb.ToASCIIString(eol));
            Assert.AreEqual(BucketEol.Zero, eol);

            var b = MakeBucket("a", "b", "c", "\0a", "bc", "d\0a", "b", "c", "\0", "a");

            (bb, eol) = await b.ReadUntilEolFullAsync(BucketEol.Zero, new BucketEolState());

            Assert.AreEqual(4, bb.Length);
            Assert.AreEqual("abc", bb.ToASCIIString(eol));
            Assert.AreEqual(BucketEol.Zero, eol);

            (bb, eol) = await b.ReadUntilEolFullAsync(BucketEol.Zero, new BucketEolState());
            Assert.AreEqual(5, bb.Length);
            Assert.AreEqual("abcd", bb.ToASCIIString(eol));
            Assert.AreEqual(BucketEol.Zero, eol);

            (bb, eol) = await b.ReadUntilEolFullAsync(BucketEol.Zero, new BucketEolState());

            Assert.AreEqual(4, bb.Length);
            Assert.AreEqual("abc", bb.ToASCIIString(eol));
            Assert.AreEqual(BucketEol.Zero, eol);

            var r = MakeBucket("a", "b", "c", "\0");
            var total = "";
            var state = new BucketEolState();
            while (true)
            {
                (bb, eol) = await r.ReadUntilEolFullAsync(BucketEol.Zero, state);

                if (bb.IsEof)
                    break;

                total += "|" + bb.ToASCIIString();

                if (eol != BucketEol.None)
                    total += "!";
            }

            Assert.AreEqual("abc\0", total.Replace("|", "").Replace("!", ""));
            Assert.AreEqual("|abc\0!", total);

            r = MakeBucket("a\r\nb\rcd\r", "\nefg\rhi\r\n", "j\r", "\rk");

            total = "";
            state = new BucketEolState();
            while (true)
            {
                (bb, eol) = await r.ReadUntilEolFullAsync(BucketEol.AnyEol, state);

                if (bb.IsEof)
                    break;

                total += "|" + bb.ToASCIIString(eol) + $"[{eol}]";
            }

            Assert.AreEqual("|a[CRLF]|b[CR]|cd[CRLF]|efg[CR]|hi[CRLF]|j[CR]|[CR]|k[None]",
                            total.Replace("\r", "/r/"));

            r = MakeBucket("a\r");
            total = "";
            state = new BucketEolState();
            while (true)
            {
                (bb, eol) = await r.ReadUntilEolFullAsync(BucketEol.AnyEol, state);

                if (bb.IsEof)
                    break;

                total += "|" + bb.ToASCIIString(eol) + $"[{eol}]";
            }

            Assert.AreEqual("|a[CR]",
                            total.Replace("\r", "/r/"));


            r = MakeBucket("H", "T", "T", "P", "/", "1", ".", "1", "\r", "\n", "a");

            total = "";
            state = new BucketEolState();
            while (true)
            {
                (bb, eol) = await r.ReadUntilEolFullAsync(BucketEol.AnyEol, state);

                if (bb.IsEof)
                    break;

                total += "|" + bb.ToASCIIString(eol) + $"[{eol}]";
            }

            Assert.AreEqual("|HTTP/1.1[CRLF]|a[None]",
                            total.Replace("\r", "/r/"));
        }

        [TestMethod]
        public async Task EnumFew()
        {
            var list = Enumerable.Range(0, 22).ToArray();

            var vq = list.AsAsyncQueryable();

            // Legacy walk
            foreach (var v in vq)
            {

            }

            Assert.AreEqual(22, vq.Count());

            // Wrapped async walk
            await foreach (var v in vq)
            {

            }

            // Filter (compile time duplicate check)
            await foreach (var v in vq.Where(x => x > 5))
            {

            }

            // Selection (compile time duplicate check)
            await foreach (var v in vq.Select(x => x + 12))
            {

            }

            // Usable by async linq
            await foreach (var v in vq.WhereAwait(async x => await Task.FromResult(x) == 12))
            {

            }

            await foreach (var v in vq.OrderByDescending(x => x))
            {

            }
        }

        [TestMethod]
        public void TestCommitChainValues()
        {
            for (int i = 0; i < 10; i++)
            {
                GitCommitChainValue cv = new GitCommitChainValue(i, DateTime.Now);

                Assert.AreEqual(i, cv.Generation);
                cv = GitCommitChainValue.FromValue(cv.Value);

                Assert.AreEqual(i, cv.Generation);
            }

            for (int i = 1970; i < 2100; i++)
            {
                GitCommitChainValue cv = new GitCommitChainValue(i, new DateTime(i, 2, 2, 0, 0, 0, DateTimeKind.Utc));

                Assert.AreEqual(i, cv.Generation);
                Assert.AreEqual(new DateTimeOffset(i, 2, 2, 0, 0, 0, TimeSpan.Zero), cv.CorrectedTime);

                // Values are together stored in an ulong, so recreate from there and test again
                cv = GitCommitChainValue.FromValue(cv.Value);

                Assert.AreEqual(i, cv.Generation);
                Assert.AreEqual(new DateTimeOffset(i, 2, 2, 0, 0, 0, TimeSpan.Zero), cv.CorrectedTime);
            }
        }


        private Bucket MakeBucket(params string[] args)
        {
            return new AggregateBucket(args.Select(x => Encoding.ASCII.GetBytes(x).AsBucket()).ToArray());
        }

        private string FormatHash(byte[] hashResult)
        {
            var sb = new StringBuilder();
            foreach (var b in hashResult)
                sb.Append(b.ToString("X2"));

            return sb.ToString();
        }
    }
}
