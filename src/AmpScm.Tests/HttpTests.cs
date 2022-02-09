using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
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
            using var br = BucketWebRequest.Create("https://github.com/get-404");

            br.Headers[HttpRequestHeader.Connection] = "close";
            br.Headers[HttpRequestHeader.UserAgent] = "BucketTest/0 " + TestContext.TestName;
            var result = await br.GetResponseAsync();

            BucketBytes bb;
            string total = "";
            int len = 0;

            while (!(bb = await result.ReadAsync()).IsEof)
            {
                var t = bb.ToUTF8String();
                len += bb.Length;
                Console.WriteLine(t);
                total += t;
            }
        }

        [TestMethod]
        public async Task GetGitHubHomeInsecure()
        {
            using var br = BucketWebRequest.Create("http://github.com/get-404");

            br.Headers[HttpRequestHeader.Connection] = "close";
            br.Headers[HttpRequestHeader.UserAgent] = "BucketTest/0 " + TestContext.TestName;
            var result = await br.GetResponseAsync();

            BucketBytes bb;
            string total = "";
            int len = 0;

            while (!(bb = await result.ReadAsync()).IsEof)
            {
                var t = bb.ToUTF8String();
                len += bb.Length;
                Console.WriteLine(t);
                total += t;
            }
        }
    }
}
