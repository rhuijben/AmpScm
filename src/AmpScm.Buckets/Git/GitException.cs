﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Git
{
    [Serializable]
    public class GitException : Exception
    {
        public GitException()
        {
        }

        public GitException(string message)
            : base(message)
        {


        }

        public GitException(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        protected GitException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }

    [Serializable]
    public class GitBucketException : GitException
    {
        public GitBucketException()
        {
        }

        public GitBucketException(string message)
            : base(message)
        {


        }

        public GitBucketException(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        protected GitBucketException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
