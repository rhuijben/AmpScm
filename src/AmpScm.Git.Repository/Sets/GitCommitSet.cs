using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Amp.Git.Sets
{
    public class GitCommitSet : GitSet<GitCommit>
    {
        public GitCommitSet(GitRepository repository, Expression<Func<GitSet<GitCommit>>> expression) 
            : base(repository, expression)
        {
        }

        //public override IEnumerator<GitCommit> GetEnumerator()
        //{
        //    yield return new GitCommit();
        //    yield return new GitCommit();
        //    yield return new GitCommit();
        //    yield return new GitCommit();
        //
        //}
    }
}
