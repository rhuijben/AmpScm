using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Git;

namespace AmpScm.Git
{
    public class GitBlob : GitObject
    {
        private GitBucket? _rdr;

        public sealed override GitObjectType Type => GitObjectType.Blob;

        public GitBlob(GitRepository repository, GitObjectId id)
            : base(repository, id)
        {
        }

        public GitBlob(GitRepository repository, GitBucket rdr, GitObjectId id) : this(repository, id)
        {
            _rdr = rdr;
        }
    }
}
