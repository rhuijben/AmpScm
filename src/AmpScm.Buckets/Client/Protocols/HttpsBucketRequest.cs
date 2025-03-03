﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Buckets.Client.Protocols
{
    internal class HttpsBucketRequest : HttpBucketRequest
    {
        public HttpsBucketRequest(Client.BucketWebClient client, Uri uri)
            : base(client, uri, true)
        {
        }

        private protected override async ValueTask<(Bucket Reader, IBucketWriter Writer)> CreateChannel()
        {
            var (b, w) = await base.CreateChannel().ConfigureAwait(false);

            var tls = new TlsBucket(b, w, RequestUri.Host);

            return (tls, tls);
        }

        private protected override BucketChannel CreateChannel(Bucket reader, IBucketWriter writer)
        {
            return new HttpsBucketChannel(Client, RequestUri.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped), reader, writer);
        }
    }
}
