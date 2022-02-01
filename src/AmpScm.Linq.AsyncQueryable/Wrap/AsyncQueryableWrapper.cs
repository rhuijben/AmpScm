using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Amp.Linq.AsyncQueryable.Wrap
{
    internal sealed class AsyncQueryableWrapper<T> : IAsyncQueryable<T>, IOrderedAsyncQueryable<T>
    {
        AsyncQueryableProviderWrapper AsyncProvider { get; }
        IQueryable<T> InnerQueryable { get; }

        public AsyncQueryableWrapper(IQueryable<T> inner, AsyncQueryableProviderWrapper p)
        {
            InnerQueryable = inner;
            AsyncProvider = p;
        }

        public AsyncQueryableWrapper(IQueryable<T> inner)
            : this(inner, new AsyncQueryableProviderWrapper(inner?.Provider ?? throw new ArgumentNullException(nameof(inner))))
        {
        }

        public Type ElementType => typeof(T);

        public Expression Expression => InnerQueryable.Expression;

        public IQueryProvider Provider => AsyncProvider;

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            foreach(var v in this)
            {
                yield return v;
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            return InnerQueryable.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    internal sealed class AsyncQueryableProviderWrapper : IAsyncQueryProvider, IQueryProvider
    {
        IQueryProvider QueryProvider { get; }

        public AsyncQueryableProviderWrapper(IQueryProvider provider)
        {
            QueryProvider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public IQueryable CreateQuery(Expression expression)
        {
            var q = QueryProvider.CreateQuery(expression);
            var el = q.ElementType;

            var m = AmpAsyncQueryable.GetMethod<object>(x => CreateQuery<object>(null!));
            return (IQueryable)m.MakeGenericMethod(el).Invoke(this, new object[] { expression })!;
        }

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            var q = QueryProvider.CreateQuery<TElement>(expression);
            var p = q.Provider;

            return new AsyncQueryableWrapper<TElement>(q,
                    ReferenceEquals(p, QueryProvider) ? this : new AsyncQueryableProviderWrapper(p));
        }

        public object? Execute(Expression expression)
        {
            return QueryProvider.Execute(expression);
        }

        public TResult Execute<TResult>(Expression expression)
        {
            return QueryProvider.Execute<TResult>(expression);
        }
    }
}
