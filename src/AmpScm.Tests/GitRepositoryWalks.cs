using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Git;
using AmpScm.Git.Client;
using AmpScm.Git.Client.Plumbing;
using AmpScm.Git.References;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AmpScm.Tests
{
    [TestClass]
    public class GitRepositoryWalks
    {
        public TestContext TestContext { get; set; } = null!;

        static List<string> TestRepositories { get; } = GetTestRepositories().ToList();

        static IEnumerable<string> GetTestRepositories()
        {
            string p = Path.GetDirectoryName(typeof(GitRepositoryWalks).Assembly.Location)!;

            do
            {
                if (p != Path.GetPathRoot(p) && Directory.Exists(Path.Combine(p, ".git")))
                    yield return p;
                else if (Directory.Exists(Path.Combine(p, "git-testrepos")))
                {
                    foreach (var d in Directory.GetDirectories(Path.Combine(p, "git-testrepos"), "*-*"))
                    {
                        yield return d;
                    }
                }
            }
            while ((p = Path.GetDirectoryName(p)!) != null);
        }

        public static IEnumerable<object[]> TestRepositoryArgs => TestRepositories.Select(x => new object[] { x });


        [TestMethod]
        [DynamicData(nameof(TestRepositoryArgs))]
        public async Task CanOpenRepository(string path)
        {
            using var repo = GitRepository.Open(path);
            Console.WriteLine($"Looking at {repo}");
            Console.Write($" from {repo.Remotes["origin"]?.Url}");

            if (repo.IsBare)
                Console.Write($" [bare]");
            if (repo.IsLazy)
                Console.Write($" [lazy-loaded]");
            Console.WriteLine();

            Assert.IsTrue(repo.Remotes.Any(), "Has remotes");
            Assert.IsTrue(repo.Remotes.Any(x => x.Name == "origin"), "Has origin remote");

            Assert.IsTrue(repo.Commits.Any(), "Has commits");
            if (!repo.IsLazy)
            {
                Assert.IsTrue(repo.Trees.Any(), "Has trees");
                Assert.IsTrue(repo.Blobs.Any(), "Has blobs");
                //Assert.IsTrue(repo.Tags.Any(), "Has tags");
            }

            Assert.IsNotNull(repo.Head, "Repository has an HEAD");
            Assert.IsTrue(repo.Head is GitSymbolicReference, "HEAD is an Symbolic reference");
            Assert.IsNotNull(repo.Head?.Commit, "Head can be resolved");
            Console.WriteLine($"Last change: {repo.Head.Commit.Author}");

            await foreach (var r in repo.References)
            {
                Console.WriteLine($"{r.ShortName.PadRight(15)} - {r.Commit?.Id:x7} - {r.Commit?.Author}");
            }

            Assert.IsNotNull(repo.Commits.FirstOrDefault(x => x.Parents.Count > 1), "Repository has merges");
        }

        [TestMethod]
        [DynamicData(nameof(TestRepositoryArgs))]
        public async Task WalkHistory(string path)
        {
            using var repo = GitRepository.Open(path);

            if (repo.IsShallow || repo.IsLazy)
                return;

            var r = await repo.GetPlumbing().RevisionList(new GitRevisionListArgs { MaxCount = 32, FirstParentOnly = true }).ToListAsync();

            Assert.AreEqual(32, r.Count);
            Assert.AreEqual(32, r.Count(x => x != null));
            Assert.AreEqual(32, r.Distinct().Count());

            var revs = repo.Head.Revisions.Take(32).Select(x => x.Commit.Id).ToList();

            if (!r.SequenceEqual(revs))
            {
                int? nDiff = null;
                for (int i = 0; i < Math.Min(revs.Count, r.Count); i++)
                {
                    Console.WriteLine($"{i:00} {r[i]} - {revs[i]}");

                    if (!nDiff.HasValue && r[i] != revs[i])
                        nDiff = i;
                }
                Assert.Fail($"Different list at {nDiff}");
            }

        }
    }
}
