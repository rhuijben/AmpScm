using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Git.Buckets;
using AmpScm.Buckets.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AmpScm.Tests
{
    [TestClass]
    public class HttpTests
    {
        public TestContext TestContext { get; set; } = null!;



        [TestMethod]
        public async Task GetGitHubHome()
        {
            using var br = BucketWebRequest.Create($"https://github.com/get-404-{Guid.NewGuid()}");

            //br.Headers[HttpRequestHeader.Connection] = "close";
            br.Headers[HttpRequestHeader.UserAgent] = "BucketTest/0 " + TestContext.TestName;
            var result = await br.GetResponseAsync();

            BucketBytes bb;
            string total = "";
            int len = 0;
            bool first = true;

            while (!(bb = await result.ReadAsync()).IsEof)
            {
                if (first && result is HttpResponseBucket hrb)
                {
                    TestContext.WriteLine($"HTTP/1.1 {hrb.HttpStatus} {hrb.HttpMessage}");
                    foreach (string h in hrb.ResponseHeaders)
                    {
                        TestContext.WriteLine($"{h}: {hrb.ResponseHeaders[h]}");
                    }
                    TestContext.WriteLine();
                    first = false;
                }

                var t = bb.ToUTF8String();
                len += bb.Length;
                TestContext.Write(t);
                total += t;
            }
            TestContext.Write("|\r\n");
        }

        [TestMethod]
        public async Task GetGitHubHomeInsecure()
        {
            using var br = BucketWebRequest.Create($"http://github.com/get-404-{Guid.NewGuid()}");

            br.Headers[HttpRequestHeader.Connection] = "close";
            br.Headers[HttpRequestHeader.UserAgent] = "BucketTest/0 " + TestContext.TestName;
            var result = await br.GetResponseAsync();

            BucketBytes bb;
            string total = "";
            int len = 0;
            bool first = true;

            while (!(bb = await result.ReadAsync()).IsEof)
            {
                if (first && result is HttpResponseBucket hrb)
                {
                    TestContext.WriteLine($"HTTP/1.1 {hrb.HttpStatus} {hrb.HttpMessage}");
                    foreach (string h in hrb.ResponseHeaders)
                    {
                        TestContext.WriteLine($"{h}: {hrb.ResponseHeaders[h]}");
                    }
                    TestContext.WriteLine();
                    first = false;
                }
                var t = bb.ToUTF8String();
                len += bb.Length;
                TestContext.WriteLine(t);
                total += t;
            }
        }

        [TestMethod]
        public async Task GetGitInfoV1()
        {
            using var br = BucketWebRequest.Create($"https://github.com/rhuijben/tt-parser.git/info/refs?service=git-upload-pack");

            br.Headers[HttpRequestHeader.UserAgent] = "BucketTest/0 " + TestContext.TestName;
            //br.Headers["Git-Protocol"] = "version=2";
            var result = await br.GetResponseAsync();

            BucketBytes bb;
            string total = "";
            int len = 0;
            bool first = true;

            var pkt = new GitPacketBucket(result);

            while (!(bb = await pkt.ReadFullPacket()).IsEof)
            {
                if (first && result is HttpResponseBucket hrb)
                {
                    TestContext.WriteLine($"HTTP/1.1 {hrb.HttpStatus} {hrb.HttpMessage}");
                    foreach (string h in hrb.ResponseHeaders)
                    {
                        TestContext.WriteLine($"{h}: {hrb.ResponseHeaders[h]}");
                    }
                    TestContext.WriteLine();

                    first = false;
                }

                TestContext.WriteLine($"-- {pkt.CurrentPacketLength} --");

                var t = bb.ToUTF8String();
                len += bb.Length;
                TestContext.WriteLine(t);
                total += t;
            }
        }

        [TestMethod]
        public async Task GetGitInfoV2()
        {
            using var br = BucketWebRequest.Create($"https://github.com/rhuijben/tt-parser.git/info/refs?service=git-upload-pack");

            br.Headers[HttpRequestHeader.UserAgent] = "BucketTest/0 " + TestContext.TestName;
            br.Headers["Git-Protocol"] = "version=2";
            var result = await br.GetResponseAsync();

            BucketBytes bb;
            string total = "";
            int len = 0;
            bool first = true;

            var pkt = new GitPacketBucket(result);

            while (!(bb = await pkt.ReadFullPacket()).IsEof)
            {
                if (first && result is HttpResponseBucket hrb)
                {
                    TestContext.WriteLine($"HTTP/1.1 {hrb.HttpStatus} {hrb.HttpMessage}");
                    foreach (string h in hrb.ResponseHeaders)
                    {
                        TestContext.WriteLine($"{h}: {hrb.ResponseHeaders[h]}");
                    }
                    TestContext.WriteLine();

                    first = false;
                }

                TestContext.WriteLine($"-- {pkt.CurrentPacketLength} --");

                var t = bb.ToUTF8String();
                len += bb.Length;
                TestContext.WriteLine(t);
                total += t;
            }
        }

        [TestMethod]
        public async Task GetGitInfoV2Auth()
        {
            using var br = BucketWebRequest.Create($"https://github.com/rhuijben/asd-admin-css.git/info/refs?service=git-upload-pack");

            br.Headers[HttpRequestHeader.UserAgent] = "BucketTest/0 " + TestContext.TestName;
            br.Headers["Git-Protocol"] = "version=2";
            var result = await br.GetResponseAsync();

            BucketBytes bb;
            string total = "";
            int len = 0;
            bool first = true;

            var pkt = new GitPacketBucket(result);

            while (!(bb = await result.ReadAsync()).IsEof)
            {
                if (first && result is HttpResponseBucket hrb)
                {
                    TestContext.WriteLine($"HTTP/1.1 {hrb.HttpStatus} {hrb.HttpMessage}");
                    foreach (string h in hrb.ResponseHeaders)
                    {
                        TestContext.WriteLine($"{h}: {hrb.ResponseHeaders[h]}");
                    }
                    TestContext.WriteLine();
                    first = false;
                }
                var t = bb.ToUTF8String();
                len += bb.Length;
                TestContext.WriteLine(t);
                total += t;
            }
        }
    }
}
