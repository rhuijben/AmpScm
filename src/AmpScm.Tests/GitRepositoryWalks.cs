using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Git;
using AmpScm.Git.References;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AmpScm.BucketTests
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

            if (repo.IsBare)
                Console.WriteLine($"{repo.FullPath} is bare");
            if (repo.IsLazy)
                Console.WriteLine($"{repo.FullPath} has promisor");

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

            await foreach(var r in repo.References)
            {
                Console.WriteLine($"{r.ShortName} - {r.Commit?.Id:x7} - {r.Commit?.Author}");
            }
        }

    }
}
