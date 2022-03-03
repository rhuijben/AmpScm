using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Sets
{
    internal class GitBranchesSet : GitNamedSet<GitBranch>
    {
        internal GitBranchesSet(GitRepository repository, Expression<Func<GitNamedSet<GitBranch>>> rootExpression) : base(repository, rootExpression)
        {
        }
    }
}
