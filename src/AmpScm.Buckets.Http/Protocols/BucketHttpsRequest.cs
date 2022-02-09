using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Buckets.Protocols
{
    internal class BucketHttpsRequest : BucketHttpRequest
    {
        public BucketHttpsRequest(Uri uri)
            : base(uri, true)
        {
        }

        protected override async ValueTask<(Bucket, IBucketWriter)> CreateChannel()
        {
            var (b, w) = await base.CreateChannel();

            var tls = new TlsBucket(b, w, RequestUri.Host);

            return (tls, tls);
        }
    }
}
