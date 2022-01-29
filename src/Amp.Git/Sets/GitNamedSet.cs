using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amp.Git.Implementation;

namespace Amp.Git.Sets
{
    public class GitNamedSet<T> : GitSet, IGitAsyncQueryable<T>
        where T : class, IGitNamedObject
    {
        protected GitRepository Repository { get; }
        protected MemberExpression RootExpression { get; }

        internal GitNamedSet(GitRepository repository, Expression<Func<GitNamedSet<T>>> rootExpression)
        {
            Repository = repository ?? throw new ArgumentNullException(nameof(repository));
            RootExpression = (rootExpression?.Body as MemberExpression) ?? throw new ArgumentNullException();
        }

        public IAmpGitAsyncQueryProvider Provider => Repository.SetQueryProvider;

        public Type ElementType => typeof(GitReference);

        Expression IQueryable.Expression => RootExpression;

        bool IListSource.ContainsListCollection => false;

        IQueryProvider IQueryable.Provider => Repository.SetQueryProvider;

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return Repository.SetQueryProvider.GetNamedAsyncEnumerator<T>(cancellationToken);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return Repository.SetQueryProvider.GetNamedEnumerable<T>().GetEnumerator();
        }

        public IList GetList()
        {
            return Repository.SetQueryProvider.GetNamedList<GitReference>();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public ValueTask<T?> GetAsync(string name)
        {
            return Repository.SetQueryProvider.GetNamedAsync<T>(name);
        }

        public T? this[string name]
        {
            get => Repository.SetQueryProvider.GetNamedAsync<T>(name).Result;
        }
    }
}
