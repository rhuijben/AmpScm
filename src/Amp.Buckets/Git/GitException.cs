using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Amp.Buckets.Git
{
    [Serializable]
    public class GitException : Exception
    {
        public GitException(string message)
            : base(message)
        {


        }

        public GitException(string message, Exception innerexception)
            : base(message, innerexception)
        {

        }

        protected GitException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }

    [Serializable]
    public class GitBucketException : GitException
    {
        public GitBucketException(string message)
            : base(message)
        {


        }

        public GitBucketException(string message, Exception innerexception)
            : base(message, innerexception)
        {

        }

        protected GitBucketException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
