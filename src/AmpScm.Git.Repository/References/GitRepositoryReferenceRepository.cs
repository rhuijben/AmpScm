using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.References
{
    internal class GitRepositoryReferenceRepository : GitReferenceRepository
    {
        public GitRepositoryReferenceRepository(GitRepository gitRepository, string gitDir)
            : base(gitRepository, gitDir)
        {
        }

        public override IAsyncEnumerable<GitReference> GetAll()
        {
            throw new NotImplementedException();
        }

        protected internal override ValueTask<GitReference?> GetUnsafe(string name)
        {
            throw new NotImplementedException();
        }
    }
}
