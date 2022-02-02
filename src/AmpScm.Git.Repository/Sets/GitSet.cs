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
    public interface IGitObject
    {
        ValueTask ReadAsync();
    }

    public interface IGitOidObject : IGitObject
    {
        GitId Id { get; }
    }

    public interface IGitNamedObject : IGitObject
    {
        public string Name { get; }
    }

    public class GitSet
    {
        internal GitSet()
        { }
    }

    public class GitSet<T> : GitSet, IGitAsyncQueryable<T>, IListSource
        where T : GitObject
    {
        protected GitRepository Repository { get; }
        protected MemberExpression RootExpression {get;}

        internal GitSet(GitRepository repository, Expression<Func<GitSet<T>>> rootExpression)
        {
            Repository = repository ?? throw new ArgumentNullException(nameof(repository));
            RootExpression = (rootExpression?.Body as MemberExpression) ?? throw new ArgumentNullException();
        }

        internal GitQueryProvider Provider => Repository.SetQueryProvider;

        public Type ElementType => typeof(T);

        Expression IQueryable.Expression => RootExpression;

        bool IListSource.ContainsListCollection => false;

        IQueryProvider IQueryable.Provider => Repository.SetQueryProvider;

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return Repository.SetQueryProvider.GetAsyncEnumerator<T>(cancellationToken);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return Repository.SetQueryProvider.GetEnumerable<T>().GetEnumerator();
        }

        public IList GetList()
        {
            return Repository.SetQueryProvider.GetList<T>();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public ValueTask<T?> GetAsync(GitId id)
        {
            return Repository.SetQueryProvider.GetAsync<T>(id);
        }

        public T? this[GitId id]
        {
            get => Repository.SetQueryProvider.GetAsync<T>(id).Result;
        }
    }
}
