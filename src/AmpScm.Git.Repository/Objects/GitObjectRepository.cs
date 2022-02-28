using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Git;
using AmpScm.Buckets.Specialized;
using AmpScm.Git.Objects;

namespace AmpScm.Git.Objects
{
    public abstract class GitObjectRepository
    {
        protected GitRepository Repository { get; }

        protected GitObjectRepository(GitRepository repository, string key)
        {
            Repository = repository ?? throw new ArgumentNullException(nameof(repository));
            Key = key ?? throw new ArgumentNullException(nameof(key));
        }

        internal virtual string Key { get; }

        public virtual IAsyncEnumerable<TGitObject> GetAll<TGitObject>()
            where TGitObject : GitObject
        {
            return AsyncEnumerable.Empty<TGitObject>();
        }


        public virtual ValueTask<TGitObject?> Get<TGitObject>(GitId oid)
            where TGitObject : GitObject
        {
            return default;
        }

        internal virtual ValueTask<GitObjectBucket?> ResolveByOid(GitId oid)
        {
            return default;
        }

        internal virtual ValueTask<IGitCommitGraphInfo?> GetCommitInfo(GitId oid)
        {
            return default;
        }

        internal virtual bool ProvidesCommitInfo => true;
        internal virtual bool ProvidesGetObject => true;

        internal GitObjectType? ObjectType<TGitObject>() where TGitObject : GitObject
        {
            if (typeof(TGitObject) == typeof(GitBlob))
                return GitObjectType.Blob;
            else if (typeof(TGitObject) == typeof(GitTree))
                return GitObjectType.Tree;
            else if (typeof(TGitObject) == typeof(GitCommit))
                return GitObjectType.Commit;
            else if (typeof(TGitObject) == typeof(GitTag))
                return GitObjectType.Tag;
            else
                return null;
        }
    }
}
