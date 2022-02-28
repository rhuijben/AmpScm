using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;

namespace AmpScm.Buckets.Client.Protocols
{
    internal class BucketChannel
    {
        internal BucketChannel(BucketWebClient client, string key, Bucket reader, IBucketWriter writer)
        {
            Client = client;
            Key = key;
            Reader = reader;
            Writer = writer;
        }

        internal string Key { get; }
        internal BucketWebClient Client { get; }
        internal Bucket Reader { get; }
        internal IBucketWriter Writer { get; }

        internal void Release()
        {
            Client.Release(this);
        }
    }
}
