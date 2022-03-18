using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

[assembly: CLSCompliant(true)]

namespace AmpScm.Linq.AsyncQueryable
{
    /// <summary>
    ///  Provides functionality to evaluate queries against a specific data source wherein
    ///  the type of the data is not specified.
    /// </summary>
#pragma warning disable CA1010 // Generic interface should also be implemented
    public interface IAsyncQueryable : IQueryable
#pragma warning restore CA1010 // Generic interface should also be implemented
    {
    }    
}
