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
        Bucket? _bucket;
        Func<ValueTask<Bucket>> _fetchBlob;

        private GitBlobWriter(Bucket bucket)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));

            _bucket = bucket;
        }

        public static GitBlobWriter CreateFrom(Bucket bucket)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));

            return new GitBlobWriter(bucket);
        }

        public override async ValueTask<GitId> WriteToAsync(GitRepository repository)
        {
            if (repository is null)
                throw new ArgumentNullException(nameof(repository));

            if (Id is null || !repository.Blobs.ContainsId(Id))
            {
                var bucket = _bucket;
                bucket ??= await _fetchBlob();
                var id = await WriteBucketAsObject(_bucket, repository).ConfigureAwait(false);

                _fetchBlob ??= async () => (await repository.Blobs.GetAsync(id).ConfigureAwait(false))!.GetBucket();
                _bucket = null;

                Id = id;
            }
            return Id;
        }

        public async ValueTask<GitBlob> WriteAndFetchAsync(GitRepository repository)
        {
            var id = await WriteToAsync(repository).ConfigureAwait(false);
            return await repository.GetAsync<GitBlob>(id).ConfigureAwait(false) ?? throw new InvalidOperationException();
        }

        internal void PutId(GitId id)
        {
            Id ??= id;
        }

        public sealed override GitObjectType Type => GitObjectType.Blob;
    }
}
