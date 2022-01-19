using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Amp.Buckets.Specialized
{
    public enum BucketCompressionAlgorithm
    {
        Deflate,
        ZLib,
#if NETCOREAPP
        Brotli
#endif
    }
}
