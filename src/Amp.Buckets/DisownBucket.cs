using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Amp.Buckets
{
    public sealed class DisownBucket : Specialized.ProxyBucket<DisownBucket>
    {
        public DisownBucket(Bucket inner) : base(inner, true)
        {
        }

        protected override DisownBucket? WrapDuplicate(Bucket duplicatedInner, bool reset)
        {
            return null; // Yes the duplicate *is* owned, otherwise it wouldn't have an owner
        }
    }
}
