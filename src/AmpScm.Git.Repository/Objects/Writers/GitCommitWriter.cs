using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;

namespace AmpScm.Git.Objects
{
    public class GitCommitWriter : GitObjectWriter, IGitPromisor<GitCommit>
    {
        public override GitObjectType Type => GitObjectType.Commit;

        public GitCommit? GitObject => throw new NotImplementedException();

        public GitTreeWriter Tree { get; set; }
        public IReadOnlyList<GitCommitWriter> Parents { get; set; }

        public GitSignature Committer { get; set; }
        public GitSignature Author { get; set; }

        private GitCommitWriter()
        {

        }

        public static GitCommitWriter Create(params GitCommitWriter[] parents)
        {
            if (parents?.Any(x=> x == null) ?? false)
                throw new ArgumentOutOfRangeException(nameof(parents));

            return new GitCommitWriter()
            {
                Parents = parents?.ToArray() ?? Array.Empty<GitCommitWriter>(),
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
            return Create(parents.Select(p => p.AsWriter()).ToArray());
        }

        public override async ValueTask<GitId> WriteAsync(GitRepository toRepository)
        {
            StringBuilder sb = new StringBuilder();

            var id = await Tree.EnsureId(toRepository);

            sb.Append($"tree {id}\n");

            foreach(var p in Parents)
            {
                id = await p.EnsureId(toRepository);
                sb.Append($"parent {id}\n");
            }

            var committer = Committer ?? toRepository.Configuration.Identity;
            var author = Author ?? toRepository.Configuration.Identity;

            sb.Append($"author {committer.AsRecord()}\n");
            sb.Append($"committer {committer.AsRecord()}\n");
            sb.Append("\n");

            var b = Encoding.UTF8.GetBytes(sb.ToString()).AsBucket();

            return Id = await WriteBucketAsObject(b, toRepository).ConfigureAwait(false);
        }

        internal void PutId(GitId id)
        {
            Id ??= id;
        }

        public async ValueTask<GitCommit> WriteAndFetchAsync(GitRepository repository)
        {
            var id = await WriteAsync(repository).ConfigureAwait(false);
            return await repository.GetAsync<GitCommit>(id).ConfigureAwait(false) ?? throw new InvalidOperationException();
        }
    }
}
