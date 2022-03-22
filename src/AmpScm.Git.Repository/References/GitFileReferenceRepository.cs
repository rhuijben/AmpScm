using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Git;
using AmpScm.Git.Repository.Implementation;

namespace AmpScm.Git.References
{
    internal class GitFileReferenceRepository : GitReferenceRepository
    {
        public GitFileReferenceRepository(GitReferenceRepository repository, string gitDir) 
            : base(repository.Repository, gitDir)
        {
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public override async IAsyncEnumerable<GitReference> GetAll()
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            string baseDir = Path.GetFullPath(GitDir);

            foreach (string file in Directory.GetFiles(Path.Combine(baseDir, "refs"), "*", SearchOption.AllDirectories))
            {
                if (file.Length > baseDir.Length+1 && file[baseDir.Length] == Path.DirectorySeparatorChar)
                {
                    string name = file.Substring(baseDir.Length+1).Replace(Path.DirectorySeparatorChar, '/');

                    yield return new GitReference(this, name, (GitId?)null);
                }
            }            
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        protected internal override async ValueTask<GitReference?> GetUnsafeAsync(string name, bool findSymbolic)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            string fileName = Path.Combine(GitDir, name);

            if (!File.Exists(fileName))
                return null;

            return new GitReference(this, name, new GitAsyncLazy<GitId?>(async () => await LoadIdFromFile(fileName).ConfigureAwait(false)));
        }

        protected internal override async ValueTask<GitReference?> ResolveAsync(GitReference gitReference)
        {
            string fileName = Path.Combine(GitDir, gitReference.Name);

            if (!File.Exists(fileName))
                return null;

            string body;
            try
            {
#if NETFRAMEWORK
                body = File.ReadAllText(fileName);
#else
                body = await File.ReadAllTextAsync(fileName).ConfigureAwait(false);
#endif
            }
            catch (FileNotFoundException)
            {
                return null;
            }

            if (body.Length > 256)
                return null; // Auch...

            if (body.StartsWith("ref:", StringComparison.Ordinal))
            {
                try
                {
                    var ob = await Repository.ReferenceRepository.GetAsync(body.Substring(4).Trim()).ConfigureAwait(false);

                    if (ob is not null)
                        return ob;
                }
                catch { }
            }
            
            return gitReference; // Not symbolic, and exists. Or error and exists
        }

        async ValueTask<GitId?> LoadIdFromFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentNullException(nameof(fileName));

            string body;
            try
            {
#if NETFRAMEWORK
                body = File.ReadAllText(fileName);
#else
                body = await File.ReadAllTextAsync(fileName).ConfigureAwait(false);
#endif
            }
            catch (FileNotFoundException)
            {
                return null;
            }

            if (body.Length > 256)
                return null; // Auch...

            if (GitId.TryParse(body, out var oid))
                return oid;
            else if (GitId.TryParse(body.Trim(), out oid))
                return oid;
            else if (body.StartsWith("ref:", StringComparison.Ordinal))
            {
                try
                {
                    var ob = await Repository.ReferenceRepository.GetAsync(body.Substring(4).Trim()).ConfigureAwait(false);

                    return ob?.Id;
                }
                catch { }
            }

            return null;
        }

        public override IAsyncEnumerable<GitReferenceChange>? GetChanges(GitReference reference)
        {
            string fileName = Path.Combine(GitDir, "logs", reference.Name);

            if (File.Exists(fileName))
                return GetChangesFromRefLogFile(fileName);

            return null;
        }

        static async IAsyncEnumerable<GitReferenceChange>? GetChangesFromRefLogFile(string fileName)
        {
            using var fb = FileBucket.OpenRead(fileName);

            using var gr = new GitReferenceLogBucket(fb);

            while(await gr.ReadGitReferenceLogRecordAsync().ConfigureAwait(false) is GitReferenceLogRecord lr)
            {
                yield return new GitReferenceChange(lr);
            }
        }
    }
}
