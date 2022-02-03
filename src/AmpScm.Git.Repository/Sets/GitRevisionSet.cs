using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AmpScm.Git.Implementation;

namespace AmpScm.Git.Sets
{
    public class GitRevisionSet : GitSet, IGitAsyncQueryable<GitRevision>, IListSource
    {
        public Type ElementType => typeof(GitRevision);

        public Expression Expression => throw new NotImplementedException();

        public IQueryProvider Provider => throw new NotImplementedException();

        public async IAsyncEnumerator<GitRevision> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            yield break;
        }

        public IEnumerator<GitRevision> GetEnumerator()
        {
            return this.AsNonAsyncEnumerable().GetEnumerator();
        }

        IList IListSource.GetList()
        {
            return new List<GitRevision>(this);
        }

        bool IListSource.ContainsListCollection => false;

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
