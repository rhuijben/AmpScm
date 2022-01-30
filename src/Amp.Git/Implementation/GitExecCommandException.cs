using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Amp.Buckets.Git;

namespace Amp.Git.Implementation
{
    [Serializable]
    public class GitExecCommandException : GitException
    {
        public GitExecCommandException()
        {
        }

        public GitExecCommandException(string message) : base(message)
        {
        }

        public GitExecCommandException(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        protected GitExecCommandException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
