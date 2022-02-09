using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Buckets.Http
{
    public abstract class ResponseBucket : WrappingBucket
    {
        protected ResponseBucket(Bucket inner) 
            : base(inner, true)
        {
        }


        public virtual ValueTask ReadHeaders()
        {
            return default;
        }
    }
}
