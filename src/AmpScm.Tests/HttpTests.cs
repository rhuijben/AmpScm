using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Client;
using AmpScm.Buckets.Git.Buckets;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AmpScm.Buckets.Client.Http;
using AmpScm.Git;

namespace AmpScm.Tests
{
    [TestClass]
    public class HttpTests
    {
        public TestContext TestContext { get; set; } = null!;

        public BucketWebClient Client { get; } = new();


        [TestCleanup]
        public void Done()
        {
            Client.Dispose();
        }


        [TestMethod]
        public async Task GetGitHubHome()
        {
            using var br = Client.CreateRequest($"https://github.com/get-404-{Guid.NewGuid()}");

            br.Headers[HttpRequestHeader.Connection] = "close";
            br.Headers[HttpRequestHeader.UserAgent] = "BucketTest/0 " + TestContext.TestName;
            var result = await br.GetResponseAsync();

            BucketBytes bb;
            string total = "";
            int len = 0;

            await result.ReadHeaders();

            if (result is HttpResponseBucket hrb)
            {
                TestContext.WriteLine($"HTTP/1.1 {hrb.HttpStatus} {hrb.HttpMessage}");
            }

            TestContext.WriteLine(result.Headers.ToString());

            while (!(bb = await result.ReadAsync()).IsEof)
            {
                var t = bb.ToUTF8String();
                len += bb.Length;
                TestContext.WriteLine(t);
                total += t;
            }
        }

        [TestMethod]
        public async Task GetGitHubHomeInsecure()
        {
            using var br = Client.CreateRequest($"http://github.com/get-404-{Guid.NewGuid()}");

            br.Headers[HttpRequestHeader.Connection] = "close";
            br.Headers[HttpRequestHeader.UserAgent] = "BucketTest/0 " + TestContext.TestName;
            var result = await br.GetResponseAsync();

            BucketBytes bb;
            string total = "";
            int len = 0;

            await result.ReadHeaders();
            if (result is HttpResponseBucket hrb)
            {
                TestContext.WriteLine($"HTTP/1.1 {hrb.HttpStatus} {hrb.HttpMessage}");

                TestContext.WriteLine(result.Headers.ToString());
            }

            while (!(bb = await result.ReadAsync()).IsEof)
            {
                var t = bb.ToUTF8String();
                len += bb.Length;
                TestContext.WriteLine(t);
                total += t;
            }
        }

        [TestMethod]
        public async Task GetGitInfoV1()
        {
            using var br = Client.CreateRequest($"https://github.com/rhuijben/tt-parser.git/info/refs?service=git-upload-pack");

            br.Headers[HttpRequestHeader.UserAgent] = "BucketTest/0 " + TestContext.TestName;
            //br.Headers["Git-Protocol"] = "version=2";
            var result = await br.GetResponseAsync();

            BucketBytes bb;
            string total = "";
            int len = 0;

            await result.ReadHeaders();
            if (result is HttpResponseBucket hrb)
            {
                TestContext.WriteLine($"HTTP/1.1 {hrb.HttpStatus} {hrb.HttpMessage}");

                TestContext.WriteLine(result.Headers.ToString());
            }

            var pkt = new GitPacketBucket(result);

            while (!(bb = await pkt.ReadFullPacket()).IsEof)
            {
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
            using var br = Client.CreateRequest($"https://github.com/rhuijben/tt-parser.git/info/refs?service=git-upload-pack");

            br.Headers[HttpRequestHeader.UserAgent] = "BucketTest/0 " + TestContext.TestName;
            br.Headers["Git-Protocol"] = "version=2";
            var result = await br.GetResponseAsync();

            BucketBytes bb;
            string total = "";
            int len = 0;

            await result.ReadHeaders();
            if (result is HttpResponseBucket hrb)
            {
                TestContext.WriteLine($"HTTP/1.1 {hrb.HttpStatus} {hrb.HttpMessage}");

                TestContext.WriteLine(result.Headers.ToString());
            }

            var pkt = new GitPacketBucket(result);

            while (!(bb = await pkt.ReadFullPacket()).IsEof)
            {
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
            using var rp = GitRepository.Open(Environment.CurrentDirectory);

            using var br = Client.CreateRequest($"https://github.com/rhuijben/asd-admin-css.git/info/refs?service=git-upload-pack");
            //br.PreAuthenticate = true;

            br.BasicAuthentication += (sender, e) => { e.Username = $"q-{Guid.NewGuid()}"; e.Password = "123"; e.Handled = true; };
            //br.BasicAuthentication += rp.Configuration.BasicAuthenticationHandler;


            br.Headers[HttpRequestHeader.UserAgent] = "BucketTest/0 " + TestContext.TestName;
            br.Headers["Git-Protocol"] = "version=2";
            var result = await br.GetResponseAsync();

            BucketBytes bb;
            string total = "";
            int len = 0;


            await result.ReadHeaders();
            if (result is HttpResponseBucket hrb)
            {
                TestContext.WriteLine($"HTTP/1.1 {hrb.HttpStatus} {hrb.HttpMessage}");

                TestContext.WriteLine(result.Headers.ToString());
            }

            var pkt = new GitPacketBucket(result);

            while (!(bb = await result.ReadAsync()).IsEof)
            {
                var t = bb.ToUTF8String();
                len += bb.Length;
                TestContext.WriteLine(t);
                total += t;
            }
        }
    }
}
