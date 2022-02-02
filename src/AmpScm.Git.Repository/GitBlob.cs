using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Git;

namespace AmpScm.Git
{
    public sealed class GitBlob : GitObject
    {
        private GitBucket? _rdr;

        public sealed override GitObjectType Type => GitObjectType.Blob;

        internal GitBlob(GitRepository repository, GitBucket rdr, GitObjectId id)
            : base(repository, id)
        {
            _rdr = rdr;
        }
    }
}
