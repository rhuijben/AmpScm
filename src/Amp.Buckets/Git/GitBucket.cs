using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Amp.Buckets.Git
{
    public abstract class GitBucket : Specialized.WrappingBucket
    {
        protected GitBucket(Bucket inner) : base(inner)
        {
        }
    }
}
