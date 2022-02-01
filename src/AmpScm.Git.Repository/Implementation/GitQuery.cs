using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amp.Git.Sets;

namespace Amp.Git.Implementation
{
    internal class GitQuery<T> : IQueryable<T>, IOrderedQueryable<T>, IGitAsyncQueryable<T>
    {
        public GitQuery(GitQueryProvider provider, Expression expression)
        {
            Provider = provider ?? throw new ArgumentNullException(nameof(provider));
            Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        }

        public Type ElementType => typeof(T);

        public Expression Expression { get; }

        public GitQueryProvider Provider { get; }

        public bool ContainsListCollection => throw new NotImplementedException();

        IQueryProvider IQueryable.Provider => Provider;

        public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            foreach(var v in this)
            {
                if (v is IGitObject r)
                    await r.ReadAsync();

                yield return v;
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            return Provider.Execute<IEnumerable<T>>(Expression).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
