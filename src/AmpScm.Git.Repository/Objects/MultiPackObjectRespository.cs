using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Git;

namespace AmpScm.Git.Objects
{
    internal class MultiPackObjectRespository : GitObjectRepository
    {
        public MultiPackObjectRespository(GitRepository repository) : base(repository, "MultiPack:" + repository.GitDir)
        {
        }

        public override ValueTask<TGitObject?> GetByIdAsync<TGitObject>(GitId oid)
            where TGitObject : class
        {
            return default;
        }

        public override IAsyncEnumerable<TGitObject> GetAll<TGitObject>(HashSet<GitId> alreadyReturned)
        {
            return AsyncEnumerable.Empty<TGitObject>();
        }
    }
}
