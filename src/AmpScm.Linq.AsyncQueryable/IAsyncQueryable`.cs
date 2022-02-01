using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Amp.Linq.AsyncQueryable
{
    /// <summary>
    /// Provides functionality to evaluate queries against a specific data source wherein
    /// the type of the data is known.
    /// </summary>
    /// <typeparam name="T">The type of the data in the data source.</typeparam>
    public interface IAsyncQueryable<out T> : IQueryable<T>, IAsyncEnumerable<T>, IAsyncQueryable
    {
    }

    /// <summary>
    /// Represents the result of a sorting operation.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IOrderedAsyncQueryable<out T> : IAsyncQueryable<T>, IOrderedQueryable<T>
    {

    }
}
