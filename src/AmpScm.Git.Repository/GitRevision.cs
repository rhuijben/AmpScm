using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Git.Sets;

namespace AmpScm.Git
{
    public class GitRevision :IGitObject
    {
        internal GitRevision(GitCommit commit)
        {
            Commit = commit;
        }


        public GitCommit Commit { get; }

        public ValueTask ReadAsync()
        {
            return default;
        }
    }
}
