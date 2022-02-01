using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using AmpScm.Linq.AsyncQueryable;

namespace AmpScm.Git.Implementation
{
    public interface IGitAsyncQueryable<out T> : IQueryable<T>, IAsyncEnumerable<T>, IAsyncQueryable<T>
    {

    }
}
