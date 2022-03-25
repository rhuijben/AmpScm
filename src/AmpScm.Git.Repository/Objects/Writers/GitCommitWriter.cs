using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;

namespace AmpScm.Git.Objects
{
    public sealed class GitCommitWriter : GitObjectWriter<GitCommit>
    {
        public override GitObjectType Type => GitObjectType.Commit;

        public GitTreeWriter Tree { get; set; }
        public IReadOnlyList<IGitLazy<GitCommit>> Parents { get; set; }

        public GitSignature? Committer { get; set; }
        public GitSignature? Author { get; set; }

        public string? CommitMessage { get; set; }

        private GitCommitWriter()
        {
            Parents = default!;
            Tree = default!;
        }

        public static GitCommitWriter Create(params GitCommitWriter[] parents)
        {
            if (parents?.Any(x=> x == null) ?? false)
                throw new ArgumentOutOfRangeException(nameof(parents));

            return new GitCommitWriter()
            {
                Parents = parents?.ToArray() ?? Array.Empty<IGitLazy<GitCommit>>(),
                Tree = parents?.FirstOrDefault()?.Tree ?? GitTreeWriter.CreateEmpty()
            };
        }

        public static GitCommitWriter CreateFromTree(GitTreeWriter tree)
        {
            return new GitCommitWriter()
            {
                Tree = tree ?? throw new ArgumentNullException(nameof(tree))
            };
        }

        public static GitCommitWriter Create(params GitCommit[] parents)
        {
            if (parents?.Any(x => x == null) ?? false)
                throw new ArgumentOutOfRangeException(nameof(parents));

            return new GitCommitWriter()
            {
                Parents = parents?.ToArray() ?? Array.Empty<IGitLazy<GitCommit>>(),
                Tree = parents?.FirstOrDefault()?.Tree.AsWriter() ?? GitTreeWriter.CreateEmpty()
            };
        }

        public override async ValueTask<GitId> WriteToAsync(GitRepository repository)
        {
            if (repository is null)
                throw new ArgumentNullException(nameof(repository));

            if (Id is null || !repository.Commits.ContainsId(Id))
            {
                StringBuilder sb = new StringBuilder();

                var id = await Tree.WriteToAsync(repository).ConfigureAwait(false);

                sb.Append((string)$"tree {id}\n");

                foreach (var p in Parents)
                {
                    id = await p.WriteToAsync(repository).ConfigureAwait(false);
                    sb.Append((string)$"parent {id}\n");
                }

                var committer = Committer ?? repository.Configuration.Identity;
                var author = Author ?? repository.Configuration.Identity;

                sb.Append((string)$"author {author.AsRecord()}\n");
                sb.Append((string)$"committer {committer.AsRecord()}\n");
                // "encoding " // if not UTF-8
                // -extra headers-
                sb.Append('\n');

                var msg = CommitMessage;
                if (!string.IsNullOrWhiteSpace(msg))
                    sb.Append(msg.Replace("\r", "", StringComparison.Ordinal));

                var b = Encoding.UTF8.GetBytes(sb.ToString()).AsBucket();

                Id = await WriteBucketAsObject(b, repository).ConfigureAwait(false);
            }
            return Id;
        }
    }
}
