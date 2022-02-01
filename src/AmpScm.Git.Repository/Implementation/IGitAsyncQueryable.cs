using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Amp.Linq.AsyncQueryable;

namespace Amp.Git.Implementation
{
    public interface IGitAsyncQueryable<out T> : IQueryable<T>, IAsyncEnumerable<T>, IAsyncQueryable<T>
    {

    }
}
