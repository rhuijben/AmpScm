using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Http;
using AmpScm.Buckets.Interfaces;

namespace AmpScm.Buckets.Protocols
{
    public class BucketHttpRequest : BucketWebRequest
    {
        public BucketHttpRequest(Uri uri) : base(uri)
        {
        }

        private protected BucketHttpRequest(Uri uri, bool forHttps) : base(uri)
        {

        }

        public override async ValueTask<Bucket> GetResponseAsync()
        {
            var (r, w) = await CreateChannel();

            w.Write(CreateRequest());
            //await w.ShutdownAsync();

            return new HttpResponseBucket(r);
        }

        public Encoding RequestEncoding { get; set; } = Encoding.UTF8;

        protected virtual async ValueTask<(Bucket, IBucketWriter)> CreateChannel()
        {
            Socket s = new Socket(SocketType.Stream, ProtocolType.Tcp);

            try
            {
                var sb = new SocketBucket(s);

                await sb.ConnectAsync(RequestUri.Host, RequestUri.Port);

                return (sb, sb);
            }
            catch
            {
                s.Dispose();
                throw;
            }
        }

        protected virtual Bucket CreateRequest()
        {
            AggregateBucket bucket = new AggregateBucket();
            Encoding enc = RequestEncoding;

            bucket.Append(enc.GetBytes((Method ?? "GET") + " ").AsBucket());
            bucket.Append(enc.GetBytes(RequestUri.GetComponents(UriComponents.PathAndQuery, UriFormat.UriEscaped)).AsBucket());
            bucket.Append(enc.GetBytes(" HTTP/1.1\r\n").AsBucket());
            bucket.Append(enc.GetBytes("Host: ").AsBucket());
            bucket.Append(enc.GetBytes(RequestUri.Host).AsBucket());
            bucket.Append(enc.GetBytes("\r\n").AsBucket());

            bucket.Append(CreateHeaders());
            bucket.Append(enc.GetBytes("\r\n").AsBucket());

            return bucket;
        }

        protected virtual Bucket CreateHeaders()
        {
            AggregateBucket bucket = new AggregateBucket();
            Encoding enc = Encoding.UTF8;

            foreach (string key in Headers.Keys)
            {
                if (key.Equals("Host", StringComparison.OrdinalIgnoreCase))
                    continue;

                bucket.Append(enc.GetBytes(key).AsBucket());
                bucket.Append(enc.GetBytes(": ").AsBucket());
                bucket.Append(enc.GetBytes(Headers[key].ToString()!).AsBucket());
                bucket.Append(enc.GetBytes("\r\n").AsBucket());
            }

            return bucket;
        }
    }
}
