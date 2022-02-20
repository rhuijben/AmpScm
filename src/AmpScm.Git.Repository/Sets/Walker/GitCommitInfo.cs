using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Sets.Walker
{
    internal class GitCommitInfo : IEquatable<GitCommitInfo>
    {
        public GitCommit Commit { get; }
        public GitCommitInfo(GitCommit from)
        {
            Commit = from;
        }

        public GitId Id => Commit.Id;

        public IReadOnlyList<GitId> ParentIds => Commit.ParentIds;

        public IReadOnlyList<GitCommit> Parents => Commit.Parents!;

        public bool Equals(GitCommitInfo? other)
        {
            return other?.Id == Id;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
    }
}
