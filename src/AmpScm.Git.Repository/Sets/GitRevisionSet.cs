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
using AmpScm.Git.References;
using AmpScm.Git.Sets.Walker;

namespace AmpScm.Git.Sets
{
    public class GitRevisionSet : GitSet, IGitAsyncQueryable<GitRevision>, IListSource
    {
        protected GitRepository Repository { get; }
        GitRevisionSetOptions _options;

        internal GitRevisionSet(GitRepository repository)
            : this(repository, null)
        {
        }

        internal GitRevisionSet(GitRepository repository, GitRevisionSetOptions? options)
        {
            Repository = repository;
            _options = options ?? new GitRevisionSetOptions();
        }

        Type IQueryable.ElementType => typeof(GitRevision);

        static System.Reflection.PropertyInfo GetProperty<T>(Expression<Func<T, object>> pr)
            => (System.Reflection.PropertyInfo)((MemberExpression)pr.Body).Member;

        static System.Reflection.MethodInfo GetMethod(Expression<Func<GitRevisionSet, object>> pr)
            => ((MethodCallExpression)pr.Body).Method;

        Expression IQueryable.Expression => Expression.Call(Expression.Property(Expression.Constant(Repository), 
            GetProperty<GitRepository>(x => x.NoRevisions)), 
            GetMethod(x=> x.SetOptions(null!)),
            Expression.Constant(_options));

        IQueryProvider IQueryable.Provider => Repository.SetQueryProvider;

        async IAsyncEnumerator<GitRevision> IAsyncEnumerable<GitRevision>.GetAsyncEnumerator(CancellationToken cancellationToken)
        {
            var w = new GitRevisionWalker(_options);

            await foreach (var v in w)
            {
                yield return v;
            }
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

        internal GitRevisionSet AddReference(GitReference gitReference)
        {
            if (gitReference?.Commit is var q)
                return AddCommit(q);

            return this;
        }

        internal GitRevisionSet AddCommit(GitCommit gitCommit)
        {
            return new GitRevisionSet(Repository, _options.AddCommit(gitCommit));
        }

        internal GitRevisionSet SetOptions(GitRevisionSetOptions options)
        {
            return new GitRevisionSet(Repository, options);
        }
    }
}
