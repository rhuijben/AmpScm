using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amp.Buckets.Git;

namespace Amp.Git
{
    public class GitTag : GitObject
    {
        private GitBucket? _rdr;

        internal GitTag(GitRepository repository, GitObjectId id)
            : base(repository, id)
        {
        }

        internal GitTag(GitRepository repository, GitBucket rdr, GitObjectId id) 
            : this(repository, id)
        {
            _rdr = rdr;
        }

        public override GitObjectType Type => GitObjectType.Tag;

        public GitObject Object => null;
    }
}
