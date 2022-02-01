using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Git;

namespace AmpScm.Git.Objects
{
    public abstract class GitObjectRepository
    {
        protected GitRepository Repository { get; }

        protected GitObjectRepository(GitRepository repository)
        {
            Repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public abstract IAsyncEnumerable<TGitObject> GetAll<TGitObject>()
            where TGitObject : GitObject;


        public abstract ValueTask<TGitObject?> Get<TGitObject>(GitObjectId objectId)
            where TGitObject : GitObject;

        internal virtual ValueTask<GitBucket?> ResolveByOid(GitObjectId arg)
        {
            return default;
        }
    }
}
