using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Amp.Buckets
{
    [Serializable]
    public class BucketException : Exception
    {
        public BucketException()
        {
        }

        public BucketException(string? message) : base(message)
        {
        }

        public BucketException(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        protected BucketException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
