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
    internal class FileObjectRepository : GitObjectRepository
    {
        private string objectsDir;

        public FileObjectRepository(GitRepository repository, string objectsDir)
            : base(repository)
        {
            this.objectsDir = objectsDir;
        }

        public override async ValueTask<TGitObject?> Get<TGitObject>(GitId objectId)
            where TGitObject : class
        {
            var name = objectId.ToString();

            string path = Path.Combine(objectsDir, name.Substring(0, 2), name.Substring(2));

            if (!File.Exists(path))
                return null;

            var fileReader = FileBucket.OpenRead(path);
            try
            {
                var rdr = new GitObjectFileBucket(fileReader);

                await rdr.ReadRemainingBytesAsync();

                var r = GitObject.FromBucket(Repository, rdr, typeof(TGitObject), objectId) as TGitObject;

                if (r == null)
                    fileReader.Dispose();

                return r;
            }
            catch
            {
                fileReader.Dispose();
                throw;
            }
        }

        public override async IAsyncEnumerable<TGitObject> GetAll<TGitObject>()
        {
            foreach (string dir in Directory.GetDirectories(objectsDir, "??"))
            {
                foreach (var file in Directory.GetFiles(dir))
                {
                    string oidString = Path.GetFileName(dir) + Path.GetFileName(file);

                    if (!GitId.TryParse(oidString, out var oid))
                        continue;

                    var r= (await Get<TGitObject>(oid))!;

                    if (r != null)
                        yield return r;
                    // else bad type
                }
            }
        }
    }
}
