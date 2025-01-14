﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git
{
    [Serializable]
    public class GitRepositoryException : GitException
    {
        public GitRepositoryException()
        {

        }
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
