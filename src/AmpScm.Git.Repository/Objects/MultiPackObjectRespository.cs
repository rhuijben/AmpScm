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
        public MultiPackObjectRespository(GitRepository repository) : base(repository)
        {
        }

        public async override ValueTask<TGitObject?> Get<TGitObject>(GitObjectId objectId)
            where TGitObject : class
        {
            return null;
        }

        public async override IAsyncEnumerable<TGitObject> GetAll<TGitObject>()
        {
            yield break;
        }
    }
}
