using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Specialized;
using AmpScm.Git.Objects;

namespace AmpScm.Git.Objects
{
    public abstract class GitObjectWriter
    {
        public GitId? Id { get; private protected set; }
        private protected GitObject? WrittenObject { get; set; }

        public abstract GitObjectType Type { get; }

        public abstract ValueTask<GitId> WriteAsync(GitRepository toRepository);

        public async ValueTask<GitId> EnsureId(GitRepository repository)
        {
            if (Id is null)
                await WriteAsync(repository).ConfigureAwait(false);

            return Id;
        }

        private protected async ValueTask<GitId> WriteBucketAsObject(Bucket bucket, GitRepository repository)
        {
            string tmpFile = Guid.NewGuid().ToString() + ".tmp";
            var di = Directory.CreateDirectory(Path.Combine(repository.GitDir, "objects", "tmp"));
            var tmpFilePath = Path.Combine(di.FullName, tmpFile);
            GitId id;
            {
                using var f = File.Create(tmpFilePath);

                long? r = await bucket.ReadRemainingBytesAsync().ConfigureAwait(false);
                if (!r.HasValue)
                {
                    string innerTmp = Path.Combine(di.FullName, tmpFile) + ".pre";
                    using var tmp = File.Create(innerTmp);
                    BucketBytes bb;
                    r = 0;
                    while (!(bb = await bucket.ReadAsync().ConfigureAwait(false)).IsEof)
                    {
#if !NETFRAMEWORK
                        await tmp.WriteAsync(bb.Memory).ConfigureAwait(false);
#else
                        var buf = bb.ToArray();
                        await tmp.WriteAsync(buf, 0, buf.Length).ConfigureAwait(false);
#endif
                        r += bb.Length;
                    }
                    bucket = FileBucket.OpenRead(innerTmp);
                }

                byte[]? checksum = null;
                using (var wb = Type.CreateHeader(r.Value!).Append(bucket).SHA1(cs => checksum = cs).Compress(BucketCompressionAlgorithm.ZLib))
                {
                    BucketBytes bb;
                    r = 0;
                    while (!(bb = await wb.ReadAsync().ConfigureAwait(false)).IsEof)
                    {
#if !NETFRAMEWORK
                        await f.WriteAsync(bb.Memory).ConfigureAwait(false);
#else
                        var buf = bb.ToArray();
                        await f.WriteAsync(buf, 0, buf.Length).ConfigureAwait(false);
#endif
                    }
                }

                id = new GitId(repository.InternalConfig.IdType, checksum!);
            }

            string idName = id.ToString();

            var dir = Path.Combine(repository.GitDir, "objects", idName.Substring(0, 2));
            Directory.CreateDirectory(dir);

            string newName = Path.Combine(dir, idName.Substring(2));
            if (File.Exists(newName))
                File.Delete(tmpFilePath);
            else
                File.Move(tmpFilePath, newName);
            return id;
        }
    }
}
