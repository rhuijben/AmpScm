using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amp.Buckets.Git;

namespace Amp.Git
{
    public class GitTree : GitObject
    {
        private GitBucket? _rdr;

        public GitTree(GitRepository repository, GitObjectId id)
            : base(repository, id)
        {
        }

        public GitTree(GitRepository repository, GitBucket rdr, GitObjectId id) 
            : this(repository, id)
        {
            _rdr = rdr;
        }
    }
}
