using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Amp.Git
{
    [Serializable]
    public class GitRepositoryException : Amp.Buckets.Git.GitException
    {
        public GitRepositoryException(string message) : base(message)
        {
        }

        public GitRepositoryException(string message, Exception innerexception) : base(message, innerexception)
        {
        }

        protected GitRepositoryException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
