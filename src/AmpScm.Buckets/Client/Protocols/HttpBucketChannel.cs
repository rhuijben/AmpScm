using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;

namespace AmpScm.Buckets.Client.Protocols
{
    internal class HttpBucketChannel : BucketChannel
    {
        internal HttpBucketChannel(BucketWebClient client, string key, Bucket reader, IBucketWriter writer) : base(client, key, reader, writer)
        {
        }
    }
}
