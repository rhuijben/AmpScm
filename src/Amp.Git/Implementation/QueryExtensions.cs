using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Amp.Git.Implementation
{
    internal static class QueryExtensions
    {
        public static IEnumerable<TResult> AsNonAsyncEnumerable<TResult>(this IAsyncEnumerable<TResult> source)
        {
            var e = source.GetAsyncEnumerator();
            try
            {
                bool next;
                do
                {
                    var r = e.MoveNextAsync().AsTask(); // Store as object instead of struct, as we are yield'ing.
                    next = r.Result;

                    if (next)
                        yield return e.Current;
                }
                while (next);
            }
            finally
            {
                if (e is IDisposable d)
                    d.Dispose();
                else
                {
                    var r2 = e.DisposeAsync().AsTask(); // Store as object instead of struct, as we are yield'ing.

                    if (!r2.IsCompleted)
                        r2.Wait();
                }
            }
        }
    }
}
