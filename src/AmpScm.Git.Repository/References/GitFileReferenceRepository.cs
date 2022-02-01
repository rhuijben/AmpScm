using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Git.Repository.Implementation;

namespace AmpScm.Git.References
{
    internal class GitFileReferenceRepository : GitReferenceRepository
    {
        public GitFileReferenceRepository(GitReferenceRepository repository, string gitDir) 
            : base(repository.Repository, gitDir)
        {
        }

        public async override IAsyncEnumerable<GitReference> GetAll()
        {
            yield break;
        }

        protected internal async override ValueTask<GitReference?> GetUnsafeAsync(string name, bool findSymbolic)
        {
            string fileName = Path.Combine(GitDir, name);

            if (!File.Exists(fileName))
                return null;

            return new GitReference(this, name, new GitAsyncLazy<GitObjectId?>(async () => await LoadOidFromFile(fileName)));
        }

        static async ValueTask<GitObjectId?> LoadOidFromFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentNullException(nameof(fileName));

            string body;
            try
            {
#if NETFRAMEWORK
                body = File.ReadAllText(fileName);
#else
                body = await File.ReadAllTextAsync(fileName);
#endif
            }
            catch (FileNotFoundException)
            {
                return null;
            }

            if (body.Length > 256)
                return null; // Auch...

            if (GitObjectId.TryParse(body, out var oid))
                return oid;
            else if (GitObjectId.TryParse(body.Trim(), out oid))
                return oid;

            return null;
        }
    }
}
