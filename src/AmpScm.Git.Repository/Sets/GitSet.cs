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
    public class GitSet
    {
        protected GitRepository Repository { get; }

        internal GitSet(GitRepository repository)
        {
            Repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }
    }

    public abstract class GitSet<T> : GitSet, IEnumerable<T>, IQueryable, IListSource
        where T : class, IGitObject
    {
        protected Expression Expression { get; set; } = default!;
        internal GitSet(GitRepository repository) : base(repository)
        {
        }

        Type IQueryable.ElementType => typeof(T);

        IQueryProvider IQueryable.Provider => Repository.SetQueryProvider;

        Expression IQueryable.Expression => Expression;

        bool IListSource.ContainsListCollection => false;

        public abstract IEnumerator<T> GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        IList IListSource.GetList()
        {
            return this.ToList();
        }
    }
}
