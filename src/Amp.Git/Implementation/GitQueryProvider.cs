using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Amp.Buckets.Git;
using Amp.Git.Sets;

namespace Amp.Git.Implementation
{
    internal class GitQueryProvider : IAmpGitAsyncQueryProvider, IGitQueryRoot
    {
        public GitQueryProvider(GitRepository repository)
        {
            Repository = repository;
        }

        public GitRepository Repository { get; }

        public IQueryable CreateQuery(Expression expression)
        {
            if (expression == null)
                throw new ArgumentNullException(nameof(expression));

            Type? type = GetElementType(expression.Type);

            if (type == null)
                throw new ArgumentOutOfRangeException(nameof(expression));

            return (IQueryable)Activator.CreateInstance(typeof(GitQuery<>).MakeGenericType(type), new object[] { this, expression! })!;
        }

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            return new GitQuery<TElement>(this, expression);
        }

        public object? Execute(Expression expression)
        {
            throw new NotImplementedException();
        }

        public TResult Execute<TResult>(Expression expression)
        {
            expression = new GitQueryVisitor().Visit(expression);

            return Expression.Lambda<Func<TResult>>(expression).Compile().Invoke();
        }

        public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
        {
            expression = new GitQueryVisitor().Visit(expression);

            return Expression.Lambda<Func<TResult>>(expression).Compile().Invoke();
        }

        public List<T> GetList<T>()
        {
            return new List<T>();
        }

        public IAsyncEnumerator<TResult> GetAsyncEnumerator<TResult>(CancellationToken cancellationToken = default)
            where  TResult : GitObject
        {
            return Repository.ObjectRepository.GetAll<TResult>().GetAsyncEnumerator();
        }

        public IEnumerable<TResult> GetEnumerable<TResult>()
            where TResult : GitObject
        {
            return Repository.ObjectRepository.GetAll<TResult>().AsNonAsyncEnumerable();            
        }

        public IQueryable<TResult> GetAll<TResult>() where TResult : GitObject
        {
            return GetEnumerable<TResult>().AsQueryable();
        }

        public async ValueTask<TResult?> GetAsync<TResult>(GitObjectId objectId) where TResult : GitObject
        {
            return await Repository.ObjectRepository.Get<TResult>(objectId);
        }

        internal static Type? GetElementType(Type type)
        {
            if (type.GetInterfaces().FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IEnumerable<>)) is Type enumerableType)
            {
                return enumerableType.GetGenericArguments()[0];
            }
            else
                return null;
        }        
    }
}
