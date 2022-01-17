using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Amp.Buckets
{
    public sealed class DisownBucket : Specialized.ProxyBucket
    {
        public DisownBucket(Bucket inner) : base(inner, true)
        {
        }

    }
}
