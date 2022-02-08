using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Specialized;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AmpScm.Tests
{
    [TestClass]
    public class HttpTests
    {
        [TestMethod]
        public async Task BasicGet()
        {
            Uri uri = new Uri("http://qqn.nl");

            Socket s = new Socket(SocketType.Stream, ProtocolType.Tcp);
            await s.ConnectAsync(uri.Host, uri.Port);

            using var bucket = new SocketBucket(s);

            bucket.Write(Encoding.UTF8.GetBytes($"GET / HTTP/1.0\r\nHost: {uri.Host}\r\nConnection: close\r\n\r\n").AsBucket());


            BucketBytes bb;
            
            while(!(bb= await bucket.ReadAsync()).IsEof)
            {
                Console.WriteLine(bb.ToUTF8String());
            }
        }


        [TestMethod]
        public async Task BasicGetHttps()
        {
            Uri uri = new Uri("https://qqn.nl");

            Socket s = new Socket(SocketType.Stream, ProtocolType.Tcp);
            await s.ConnectAsync(uri.Host, uri.Port);

            using var bucket = new SocketBucket(s).WithTlsClientFor(uri.Host);

            bucket.Write(Encoding.UTF8.GetBytes($"GET / HTTP/1.0\r\nHost: {uri.Host}\r\nConnection: close\r\n\r\n").AsBucket());


            BucketBytes bb;

            while (!(bb = await bucket.ReadAsync()).IsEof)
            {
                Console.WriteLine(bb.ToUTF8String());
            }
        }
    }
}
