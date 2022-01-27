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
            bool next;
            do
            {
                var r = e.MoveNextAsync();

                if (r.IsCompleted)
                    next = r.Result;
                else
                    next = r.GetAwaiter().GetResult();


                if (next)
                    yield return e.Current;
            }
            while (next);

            var r2 = e.DisposeAsync();

            if (!r2.IsCompleted)
                r2.GetAwaiter().GetResult();
        }
    }
}
