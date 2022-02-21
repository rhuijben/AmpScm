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
        GitRepository Repository {get;}

        public GitRevisionWalker(GitRevisionSetOptions options)
        {
            this.options = options;
            Repository = options.Commits.Select(x => x.Repository).First();
        }

        public async IAsyncEnumerator<GitRevision> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            GitCommit? c = null;

            AddCommits(options.Commits);
            c = options.Commits.FirstOrDefault();

            await EnsureInfo();

            while (c != null)
            {
                yield return new GitRevision(c);

                c = c.Parent;
            }
        }

        private async ValueTask EnsureInfo()
        {
            Stack<GitCommitInfo> stack = new Stack<GitCommitInfo>();

            foreach(var v in Commits.ToList())
            {
                stack.Push(v);
            }

            while(stack.TryPeek(out var c))
            {
                if (c.ChainInfo.HasValue)
                {
                    c = stack.Pop();
                    continue;
                }

                bool gotall = true;
                List<GitCommitInfo>? parents = null;
                foreach(var p in c.ParentIds)
                {
                    var pc = EnsureCommit(p);

                    if (!pc.ChainInfo.HasValue)
                    {
                        gotall = false;
                        stack.Push(pc);
                    }
                    else if (gotall)
                    {
                        parents ??= new List<GitCommitInfo>();
                        parents.Add(pc);
                    }
                }

                if (gotall)
                {
                    c = stack.Pop();

                    if (!c.ChainInfo.HasValue)
                    {
                        int generation;
                        long correctedTimestamp;
                        if (parents?.Count > 0)
                        {
                            generation = parents.Max(p => p.ChainInfo.Generation) + 1;
                            correctedTimestamp = Math.Max(parents.Max(p => p.ChainInfo.CorrectedTimeValue) + 1, await c.GetCommitTimeValue());
                        }
                        else
                        {
                            generation = 1;
                            correctedTimestamp = await c.GetCommitTimeValue();
                        }

                        c.SetChainInfo(new Objects.GitCommitGenerationValue(generation, correctedTimestamp));
                    }
                }
            }            
        }

        GitCommitInfo EnsureCommit(GitId id)
        {
            var tst = new GitCommitInfo(id, Repository);
            if (!Commits.TryGetValue(tst, out var v))
            {
                Commits.Add(v = tst);
            }
            return v;
        }

        private void AddCommits(IEnumerable<GitCommit> commits)
        {
            foreach (var v in commits)
            {
                if (v == null)
                    return;

                GitCommitInfo info = new GitCommitInfo(v);

                if (Commits.Contains(info))
                    return;

                Commits.Add(info);
            }
        }
    }
}
