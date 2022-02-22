using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Git;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Git.Objects
{
    internal sealed class FileObjectRepository : GitObjectRepository
    {
        private string objectsDir;

        public FileObjectRepository(GitRepository repository, string objectsDir)
            : base(repository)
        {
            this.objectsDir = objectsDir;
        }

        public override async ValueTask<TGitObject?> Get<TGitObject>(GitId oid)
            where TGitObject : class
        {
            var name = oid.ToString();

            string path = Path.Combine(objectsDir, name.Substring(0, 2), name.Substring(2));

            if (!File.Exists(path))
                return null;

            var fileReader = FileBucket.OpenRead(path);
            try
            {
                var rdr = new GitObjectFileBucket(fileReader);

                GitObject ob = await GitObject.FromBucket(Repository, rdr, oid);

                if (ob is TGitObject tg)
                    return tg;

                await rdr.DisposeAsync();

                return null;
            }
            catch
            {
                fileReader.Dispose();
                throw;
            }
        }

        internal override async ValueTask<GitObjectBucket?> ResolveByOid(GitId oid)
        {
            var name = oid.ToString();

            string path = Path.Combine(objectsDir, name.Substring(0, 2), name.Substring(2));

            if (!File.Exists(path))
                return null;

            var fileReader = FileBucket.OpenRead(path);
            return new GitObjectFileBucket(fileReader);
        }

        public override async IAsyncEnumerable<TGitObject> GetAll<TGitObject>()
        {
            foreach (string dir in Directory.GetDirectories(objectsDir, "??"))
            {
                string prefix = Path.GetFileName(dir);

                foreach (var file in Directory.GetFiles(dir))
                {
                    string idString = prefix + Path.GetFileName(file);

                    if (!GitId.TryParse(idString, out var id))
                        continue;

                    var fileReader = FileBucket.OpenRead(file);

                    var rdr = new GitObjectFileBucket(fileReader);

                    GitObject ob = await GitObject.FromBucket(Repository, rdr, id);

                    if (ob is TGitObject tg)
                        yield return tg;
                    else
                        await rdr.DisposeAsync();
                }
            }
        }

        internal override bool ProvidesCommitInfo => false;
    }
}
