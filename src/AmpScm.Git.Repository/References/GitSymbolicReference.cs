using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.References
{
    public class GitSymbolicReference : GitReference
    {
        object? _reference;

        internal GitSymbolicReference(GitReferenceRepository repository, string name)
            : base(repository, name, (GitId?)null)
        {
        }

        public override async ValueTask ReadAsync()
        {
            string body;

            if (_reference is null)
            {
                try
                {
#if NETFRAMEWORK
                    body = File.ReadAllText(Path.Combine(Repository.Repository.GitDir, Name));
#else
                    body = await File.ReadAllTextAsync(Path.Combine(Repository.GitDir, Name)).ConfigureAwait(false);
#endif
                }
                catch (FileNotFoundException)
                {
                    _reference = "";
                    return;
                }

                if (string.IsNullOrEmpty(body))
                {
                    _reference = "";
                    return;
                }

                if (body.StartsWith("ref: "))
                {
                    int n = body.IndexOfAny(new[] { ' ', '\t', '\r', '\n' }, 5);

                    if (n < 0)
                        n = body.Length;

                    _reference = body.Substring(5, n - 5);
                }
            }
        }

        public GitReference? Reference
        {
            get
            {
                if (_reference is null)
                    ReadAsync().GetAwaiter().GetResult();

                if (_reference is string r)
                {
                    _reference = Repository.Repository.ReferenceRepository.GetUnsafeAsync(r, false).Result ?? _reference;
                }

                return _reference as GitReference;
            }
        }

        public override GitObject? Object => Reference?.Object;

        public override GitCommit? Commit => Reference?.Commit;

        public override GitId? Id => Reference?.Id;
    }
}
