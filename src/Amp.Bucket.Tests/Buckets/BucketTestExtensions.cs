using Amp.Buckets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Amp.BucketTests.Buckets
{
    public static class BucketTestExtensions
    {
        public static Bucket PerByte(this Bucket self)
        {
            return new PerByteBucket(self);
        }
    }
}
