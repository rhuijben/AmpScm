using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amp.Git;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Amp.BucketTests
{
    [TestClass]
    public class GitRepositoryTests
    {
        public TestContext TestContext { get; set; } = null!;

        [TestMethod]
        public void WalkCommits()
        {
            using (var repo = GitRepository.Init(Path.Combine(TestContext.TestRunResultsDirectory, "Init")))
            {
                Assert.IsNotNull(repo.FullPath);
                var items = repo.Commits.ToArray();

                Assert.IsNotNull(items.Length);

                var b = repo.Commits.Any();

                Assert.IsFalse(b);

                //repo.Commits.FirstOrDefault(x => x.Tree != null);

                items = repo.Commits.Where(x => x != null).ToArray();

                Assert.IsNotNull(items.Length);
            }
        }

        [TestMethod]
        public void PathNormalize()
        {
            var sd = Environment.SystemDirectory;

            var normalized = GitTools.GetNormalizedFullPath(sd);

            Assert.AreEqual(normalized, GitTools.GetNormalizedFullPath(sd.ToUpperInvariant()));
            Assert.AreEqual(normalized, GitTools.GetNormalizedFullPath(sd.ToLowerInvariant()));
        }

        [TestMethod]
        public void OpenDev()
        {
            using var repo = GitRepository.Open(typeof(GitRepositoryTests).Assembly.Location);

            Assert.IsNotNull(repo.FullPath);
            Assert.IsFalse(repo.IsBare);

            using var repo2 = GitRepository.Open(repo.FullPath, false);
            Assert.IsNotNull(repo2.FullPath);
            Assert.IsFalse(repo2.IsBare);
            Assert.AreEqual(repo2.FullPath, repo.FullPath);

            using var repoGit = GitRepository.Open(Path.Combine(repo.FullPath, ".git"));
            Assert.IsNotNull(repoGit.FullPath);
            Assert.IsFalse(repoGit.IsBare);
            Assert.AreEqual(repoGit.FullPath, repo.FullPath);

            using var repoGit2 = GitRepository.Open(Path.Combine(repo.FullPath, ".git"), false);
            Assert.IsNotNull(repoGit2.FullPath);
            Assert.IsFalse(repoGit2.IsBare);
        }

        [TestMethod]
        public async Task WalkObjectsViaObjectRepository()
        {
            using var repo = GitRepository.Open(typeof(GitRepositoryTests).Assembly.Location);

            await foreach (var c in repo.ObjectRepository.GetAll<GitCommit>())
            {
                Console.WriteLine($"Commit {c.Id} - {GitTools.FirstLine(c.Message)}");
                if (c.Parent != null)
                    Console.WriteLine($" -parent {c.Parent?.Id} - {GitTools.FirstLine(c.Parent?.Message)}");
                Console.WriteLine($" -tree {c.Tree?.Id}");

                //if (c.Id.ToString() == "2a13daf257b049bd85c34fc76cabed82d9b1ca12")
                foreach (var v in c.Tree)
                {
                    Console.WriteLine($"   - {v.Name}");
                }
            }
        }

        [TestMethod]
        public async Task WalkObjectsAsync()
        {
            using var repo = GitRepository.Open(typeof(GitRepositoryTests).Assembly.Location);

            await foreach(var c in repo.Commits)
            {
                Console.WriteLine($"Commit {c.Id} - {GitTools.FirstLine(c.Message)}");
                if (c.Parent != null)
                    Console.WriteLine($" -parent {c.Parent?.Id} - {GitTools.FirstLine(c.Parent?.Message)}");
                Console.WriteLine($" -tree {c.Tree?.Id}");                

                //if (c.Id.ToString() == "2a13daf257b049bd85c34fc76cabed82d9b1ca12")
                foreach(var v in c.Tree)
                {
                    Console.WriteLine($"   - {v.Name}");
                }
            }
        }

        [TestMethod]
        public async Task WalkObjects()
        {
            using var repo = GitRepository.Open(typeof(GitRepositoryTests).Assembly.Location);

            foreach (var c in repo.Commits)
            {
                Console.WriteLine($"Commit {c.Id} - {GitTools.FirstLine(c.Message)}");
                if (c.Parent != null)
                    Console.WriteLine($" -parent {c.Parent?.Id} - {GitTools.FirstLine(c.Parent?.Message)}");
                Console.WriteLine($" -tree {c.Tree?.Id}");

                foreach (var v in c.Tree)
                {
                    Console.WriteLine($"   - {v.Name}");
                }
            }
        }

        [TestMethod]
        public async Task WalkOne()
        {
            using var repo = GitRepository.Open(typeof(GitRepositoryTests).Assembly.Location);

            var tree = repo.Trees.AsEnumerable().FirstOrDefault(x => x.Id.ToString() == "2a13daf257b049bd85c34fc76cabed82d9b1ca12");

            foreach (var v in tree)
            {
                Console.WriteLine($"   - {v.Name} - {v.Id}");
            }

            foreach(var v in tree.AllItems)
            {
                Console.WriteLine($"# {v.Path}");
            }
        }
    }
}
