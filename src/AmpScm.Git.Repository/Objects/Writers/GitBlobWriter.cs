using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Git.Objects
{
    public class GitBlobWriter : GitObjectWriter, IGitPromisor<GitBlob>
    {
        Bucket _bucket;

        private GitBlobWriter(Bucket bucket)
        {
            _bucket = bucket;
        }

        public static GitBlobWriter CreateFrom(Bucket bucket)
        {
            return new GitBlobWriter(bucket);
        }

        public override async ValueTask<GitId> WriteAsync(GitRepository repository)
        {
            if (repository is null)
                throw new ArgumentNullException(nameof(repository));

            return Id = await WriteBucketAsObject(_bucket, repository).ConfigureAwait(false);
        }

        public async ValueTask<GitBlob> WriteAndFetchAsync(GitRepository repository)
        {
            var id = await WriteAsync(repository).ConfigureAwait(false);
            return await repository.GetAsync<GitBlob>(id).ConfigureAwait(false) ?? throw new InvalidOperationException();
        }

        public GitBlob? Blob => GitObject;
        public GitBlob? GitObject => (GitBlob)WrittenObject;

        internal void PutId(GitId id)
        {
            Id ??= id;
        }

        public override GitObjectType Type => GitObjectType.Blob;
    }
}
