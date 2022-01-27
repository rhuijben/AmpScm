using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Amp.Git.Implementation
{
    public interface IAmpGitAsyncQueryProvider : IQueryProvider
    {
        TResult ExecuteAsync<TResult>(System.Linq.Expressions.Expression expression, System.Threading.CancellationToken cancellationToken = default);
    }

    public interface IAmpGitAsyncQueryable : IQueryable, IEnumerable
    {
        new IAmpGitAsyncQueryProvider Provider { get; }
    }

    public interface IAmpGitAsyncQueryable<out T> : IAsyncEnumerable<T>, IAmpGitAsyncQueryable
    {
    }

    public interface IGitAsyncQueryable<out T> : IQueryable<T>, IAsyncEnumerable<T>, IAmpGitAsyncQueryable<T>, IListSource
    {

    }
}
