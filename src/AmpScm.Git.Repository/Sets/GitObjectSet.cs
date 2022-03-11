using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AmpScm.Buckets.Git;
using AmpScm.Git.Implementation;

namespace AmpScm.Git.Sets
{
    public class GitObjectSet<T> : GitSet<T>, IGitAsyncQueryable<T>
        where T : GitObject
    {
        internal GitObjectSet(GitRepository repository, Expression<Func<GitObjectSet<T>>> rootExpression)
            : base(repository)
        {
            Expression = (rootExpression?.Body as MemberExpression) ?? throw new ArgumentNullException();
        }

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return Repository.SetQueryProvider.GetAsyncEnumerator<T>(cancellationToken);
        }

        public override IEnumerator<T> GetEnumerator()
        {
            return Repository.SetQueryProvider.GetEnumerable<T>().GetEnumerator();
        }

        public ValueTask<T?> GetAsync(GitId id)
        {
            return Repository.SetQueryProvider.GetAsync<T>(id);
        }

        public T? this[GitId id]
        {
            get => Repository.SetQueryProvider.GetAsync<T>(id).AsTask().Result;
        }
    }
}
