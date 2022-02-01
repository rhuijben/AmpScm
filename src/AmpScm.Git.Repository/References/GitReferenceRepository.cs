using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Amp.Git.References
{
    public abstract class GitReferenceRepository
    {
        protected GitRepository Repository { get; }
        protected string GitDir { get; }

        protected GitReferenceRepository(GitRepository repository, string gitDir)
        {
            Repository = repository ?? throw new ArgumentNullException(nameof(repository));
            GitDir = gitDir ?? throw new ArgumentNullException(nameof(gitDir));
        }


        public abstract IAsyncEnumerable<GitReference> GetAll();

        public ValueTask<GitReference?> Get(string name)
        {
            if (!ValidReferenceName(name))
                throw new ArgumentOutOfRangeException(nameof(name));

            return GetUnsafe(name);
        }

        protected internal abstract ValueTask<GitReference?> GetUnsafe(string name);

        static HashSet<char> InvalidChars = new HashSet<char>(Path.GetInvalidFileNameChars());

        private bool ValidReferenceName(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));

            char last = '\0';

            for(int i = 0; i < name.Length; last=name[i++])
            {
                if (char.IsLetterOrDigit(name, i))
                    continue;
                switch(name[i])
                {
                    case '\\':
                        return false;
                    case '.' when (last == '/' || last == '\0'):
                        return false;
                    case '/' when (last == '\0' || last == '\0'):
                        return false;
                    default:
                        if (char.IsControl(name, i) || InvalidChars.Contains(name[i]))
                            return false;
                        break;
                }
            }
            return true;
        }

        public virtual ValueTask<GitReference?> ResolveByOid(GitObjectId arg)
        {
            return default;
        }
    }
}
