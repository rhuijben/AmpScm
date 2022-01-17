using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Amp.Buckets
{
    interface IBucketAggregation
    {
        Bucket Append(Bucket bucket);
        Bucket Prepend(Bucket bucket);
    }
}
