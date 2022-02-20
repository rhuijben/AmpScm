using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AmpScm.Git.Sets.Walker
{
    internal class GitRevisionWalker : IAsyncEnumerable<GitRevision>
    {
        private GitRevisionSetOptions options;
        HashSet<GitCommitInfo> Commits { get; } = new HashSet<GitCommitInfo>();
        Queue<GitCommitInfo> ToWalk { get; } = new Queue<GitCommitInfo>();

        public GitRevisionWalker(GitRevisionSetOptions options)
        {
            this.options = options;
        }

        public async IAsyncEnumerator<GitRevision> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            GitCommit? c = null;

            await Task.Run(() =>
            {
                AddCommits(options.Commits);
                c = options.Commits.FirstOrDefault();
            });

            while (c != null)
            {
                yield return new GitRevision(c);

                c = c.Parent;
            }
        }

        private void AddCommits(IEnumerable<GitCommit> commits)
        {
            foreach(var v in commits)
            {
                DoAddCommit(v);
            }

            AddParents();
        }

        void DoAddCommit(GitCommit commit)
        {
            if (commit == null)
                return;

            GitCommitInfo info = new GitCommitInfo(commit);

            if (Commits.Contains(info))
                return;

            Commits.Add(info);
            ToWalk.Enqueue(info);
        }

        void AddParents()
        { 
            while(ToWalk.Count > 0)
            {
                var c = ToWalk.Dequeue();

                AddCommits(c.Parents);
            }            
        }
    }
}
