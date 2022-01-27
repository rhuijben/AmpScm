using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amp.Buckets.Git;

namespace Amp.Git.Objects
{
    public abstract class GitObjectRepository
    {
        protected GitRepository Repository { get; }

        protected GitObjectRepository(GitRepository repository)
        {
            Repository = repository;
        }

        public abstract IAsyncEnumerable<TGitObject> GetAll<TGitObject>()
            where TGitObject : GitObject;


        public abstract ValueTask<TGitObject?> Get<TGitObject>(GitObjectId objectId)
            where TGitObject : GitObject;

        internal ValueTask<GitBucket> ResolveByOid(GitObjectId arg)
        {
            throw new NotImplementedException();
        }
    }
}
