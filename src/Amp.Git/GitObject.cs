using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amp.Buckets.Git;

namespace Amp.Git
{
    public class GitObject
    {
        protected GitRepository Repository { get; }
        public virtual GitObjectId Id { get; }

        protected GitObject(GitRepository repository, GitObjectId id)
        {
            Repository = repository ?? throw new ArgumentNullException(nameof(repository));
            Id = id ?? throw new ArgumentNullException(nameof(id));
        }

        internal static GitObject FromBucket<TBucket>(GitRepository repository, TBucket rdr, Type type, GitObjectId id)
            where TBucket : GitBucket, IGitObjectType
        {
            switch (rdr.Type)
            {
                case GitObjectType.Commit:
                    return new GitCommit(repository, rdr, id);
                case GitObjectType.Tree:
                    return new GitTree(repository, rdr, id);
                case GitObjectType.Blob:
                    return new GitBlob(repository, rdr, id);
                case GitObjectType.Tag:
                    return new GitTag(repository, rdr, id);
                default:
                    throw new ArgumentOutOfRangeException();
            }

        }
    }
}
