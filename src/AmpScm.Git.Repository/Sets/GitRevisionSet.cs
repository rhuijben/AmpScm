﻿using System;
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

namespace AmpScm.Git.Sets
{
    public class GitRevisionSet : GitSet, IGitAsyncQueryable<GitRevision>, IListSource
    {
        private GitReferenceRepository repository;

        internal GitRevisionSet(GitReferenceRepository repository)
        {
            this.repository = repository;
        }

        Type IQueryable.ElementType => typeof(GitRevision);

        Expression IQueryable.Expression => throw new NotImplementedException();

        IQueryProvider IQueryable.Provider => throw new NotImplementedException();

        async IAsyncEnumerator<GitRevision> IAsyncEnumerable<GitRevision>.GetAsyncEnumerator(CancellationToken cancellationToken)
        {
            yield break;
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
            //throw new NotImplementedException();
            return this;
        }

        internal GitRevisionSet AddCommit(GitCommit gitCommit)
        {
            //throw new NotImplementedException();
            return this;
        }
    }
}
