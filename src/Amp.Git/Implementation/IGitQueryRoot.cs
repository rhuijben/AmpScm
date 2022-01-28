using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amp.Buckets.Git;
using Amp.Git.Sets;

namespace Amp.Git.Implementation
{
    // Internal marker interface
    internal interface IGitQueryRoot
    {
        IQueryable<TResult> GetAll<TResult>()
            where TResult : GitObject;

        ValueTask<TResult?> GetAsync<TResult>(GitObjectId objectId)
            where TResult : GitObject;
    }
}
