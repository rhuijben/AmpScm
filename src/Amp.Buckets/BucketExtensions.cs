using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Amp.Buckets
{
    public static class BucketExtensions
    {
        public static Bucket Append(this Bucket self, Bucket appendee)
        {
            if (self is IBucketAggregation col)
                return col.Append(appendee);
            else
            {
                return new AggregateBucket(self, appendee);
            }
        }

        public static Bucket Prepend(this Bucket self, Bucket newFirst)
        {
            if (self is IBucketAggregation col)
                return col.Prepend(newFirst);
            else
            {
                return new AggregateBucket(newFirst, self);
            }
        }

#if NETFRAMEWORK
        internal static string GetString(this System.Text.Encoding encoding, ReadOnlySpan<byte> bytes)
        {
            return encoding.GetString(bytes.ToArray());
        }
#endif
    }
}
