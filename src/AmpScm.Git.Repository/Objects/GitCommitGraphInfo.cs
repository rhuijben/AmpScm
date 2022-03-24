using System.Collections.Generic;
using AmpScm.Buckets.Git;

namespace AmpScm.Git.Objects
{
    internal class GitCommitGraphInfo : IGitCommitGraphInfo
    {
        public GitCommitGraphInfo(GitId[] parents, ulong chainLevel)
        {
            ParentIds = parents;
            Value = GitCommitGenerationValue.FromValue(chainLevel);
        }

        public IEnumerable<GitId> ParentIds { get; }

        public GitCommitGenerationValue Value { get; }
    }
}
