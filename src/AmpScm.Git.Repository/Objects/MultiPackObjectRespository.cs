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

        public override ValueTask<TGitObject?> Get<TGitObject>(GitObjectId objectId)
            where TGitObject : class
        {
            return default;
        }

        public override IAsyncEnumerable<TGitObject> GetAll<TGitObject>()
        {
            return AsyncEnumerable.Empty<TGitObject>();
        }
    }
}
