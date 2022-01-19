using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Amp.Buckets
{
    public interface IBucketNoClose
    {
        Bucket NoClose();
    }

    public sealed class NoCloseBucket : Specialized.ProxyBucket<NoCloseBucket>
    {
        public NoCloseBucket(Bucket inner) : base(inner, true)
        {
        }

        protected override NoCloseBucket? WrapDuplicate(Bucket duplicatedInner, bool reset)
        {
            return null; // Yes the duplicate *is* owned, otherwise it wouldn't have an owner
        }
    }
}
