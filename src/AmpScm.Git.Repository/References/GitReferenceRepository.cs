using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.References
{
    public abstract class GitReferenceRepository
    {
        public const string Head = "HEAD";

        protected internal GitRepository Repository { get; }
        protected internal string GitDir { get; }

        protected GitReferenceRepository(GitRepository repository, string gitDir)
        {
            Repository = repository ?? throw new ArgumentNullException(nameof(repository));
            GitDir = gitDir ?? throw new ArgumentNullException(nameof(gitDir));
        }


        public abstract IAsyncEnumerable<GitReference> GetAll();

        public ValueTask<GitReference?> GetAsync(string name)
        {
            if (!GitReference.ValidName(name, true))
                throw new ArgumentOutOfRangeException(nameof(name), name, "Invalid Reference name");

            return GetUnsafeAsync(name, true);
        }

        protected internal abstract ValueTask<GitReference?> GetUnsafeAsync(string name, bool findSymbolic);

        static HashSet<char> InvalidChars = new HashSet<char>(Path.GetInvalidFileNameChars());

        public virtual ValueTask<GitReference?> ResolveByOid(GitId arg)
        {
            return default;
        }
    }
}
