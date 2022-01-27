using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amp.Buckets.Git;

namespace Amp.Git.Objects
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
