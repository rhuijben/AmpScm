using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Git;
using AmpScm.Git.Client.Plumbing;
using AmpScm.Git.Objects;
using AmpScm.Git.References;
using AmpScm.Git.Sets;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AmpScm.Tests
{
    [TestClass]
    public class GitRepositoryTests
    {
        public TestContext TestContext { get; set; } = null!;

        [TestMethod]
        public void WalkCommitsEmpty()
        {
            using (var repo = GitRepository.Init(Path.Combine(TestContext.TestRunResultsDirectory, "Init")))
            {
                Assert.IsNotNull(repo.FullPath);
                var items = repo.Commits.ToArray();

                Assert.IsNotNull(items.Length);

                var b = repo.Commits.Any();

                Assert.IsFalse(b);

                //repo.Commits.FirstOrDefault(x => x.Tree != null);
                //IAsyncIListProvider
                items = repo.Commits.Where(x => x != null).ToArray();

                Assert.IsNotNull(items.Length);

                Assert.AreEqual(false, repo.Configuration.GetBool("core", "bare") ?? false);

                Assert.IsNotNull(repo.Head);
                Assert.IsNull(repo.Head.Commit);
                Assert.IsNull(repo.Head.GitObject);
            }
        }

        [TestMethod]
        public void TestBareOpen()
        {
            using (var repo = GitRepository.Init(Path.Combine(TestContext.TestRunResultsDirectory, "InitBare"), true))
            {
                Assert.IsTrue(repo.IsBare);

                var items = repo.Commits.ToArray();

                Assert.IsNotNull(items.Length);

                var b = repo.Commits.Any();

                Assert.IsFalse(b);

                //repo.Commits.FirstOrDefault(x => x.Tree != null);

                items = repo.Commits.Where(x => x != null).ToArray();

                Assert.IsNotNull(items.Length);

                Assert.AreEqual(true, repo.Configuration.GetBool("core", "bare") ?? false);
            }
        }

        [TestMethod]
        public void PathNormalize()
        {
            var sd = Environment.SystemDirectory;

            if (string.IsNullOrEmpty(sd))
                return; // Not on Windows


            var normalized = GitTools.GetNormalizedFullPath(sd);

            Assert.AreEqual(normalized, GitTools.GetNormalizedFullPath(sd.ToUpperInvariant()));
            Assert.AreEqual(normalized, GitTools.GetNormalizedFullPath(sd.ToLowerInvariant()));
        }

        [TestMethod]
        public void OpenInner()
        {
            var repoOuter = GitRepository.Init(Path.Combine(TestContext.TestRunResultsDirectory, "Inner-1"));
            using var repoInner = GitRepository.Init(Path.Combine(TestContext.TestRunResultsDirectory, "Inner-1", "Inner"));

            string path = repoInner.FullPath;

            {
                using var repo2 = GitRepository.Open(path, false);
                Assert.AreEqual(path, repo2.FullPath);
                Assert.IsFalse(repo2.IsBare);
                Assert.IsFalse(repo2.IsLazy, "Not lazy");
            }
            {
                using var repo2 = GitRepository.Open(Path.Combine(path, ".git"));
                Assert.AreEqual(path, repo2.FullPath);
                Assert.IsFalse(repo2.IsBare);
                Assert.IsFalse(repo2.IsLazy, "Not lazy");
            }
            {
                using var repo2 = GitRepository.Open(Path.Combine(path, ".git"), false);
                Assert.AreEqual(path, repo2.FullPath);
                Assert.IsFalse(repo2.IsBare);
                Assert.IsFalse(repo2.IsLazy, "Not lazy");
            }

            {
                using var repo2 = GitRepository.Open(Path.Combine(path, "a", "b", "c"), true);
                Assert.AreEqual(path, repo2.FullPath);
                Assert.IsFalse(repo2.IsBare);
                Assert.IsFalse(repo2.IsLazy, "Not lazy");
            }

            Directory.Delete(Path.Combine(repoOuter.FullPath, ".git"), true);

            {
                using var repo2 = GitRepository.Open(path, false);
                Assert.AreEqual(path, repo2.FullPath);
                Assert.IsFalse(repo2.IsBare);
                Assert.IsFalse(repo2.IsLazy, "Not lazy");
            }
            {
                using var repo2 = GitRepository.Open(Path.Combine(path, ".git"));
                Assert.AreEqual(path, repo2.FullPath);
                Assert.IsFalse(repo2.IsBare);
                Assert.IsFalse(repo2.IsLazy, "Not lazy");
            }
            {
                using var repo2 = GitRepository.Open(Path.Combine(path, ".git"), false);
                Assert.AreEqual(path, repo2.FullPath);
                Assert.IsFalse(repo2.IsBare);
                Assert.IsFalse(repo2.IsLazy, "Not lazy");
            }

            {
                using var repo2 = GitRepository.Open(Path.Combine(path, "a", "b", "c"), true);
                Assert.AreEqual(path, repo2.FullPath);
                Assert.IsFalse(repo2.IsBare);
                Assert.IsFalse(repo2.IsLazy, "Not lazy");
            }
        }

        [TestMethod]
        public async Task WalkCommitsViaObjectRepository()
        {
            using var repo = GitRepository.Open(typeof(GitRepositoryTests).Assembly.Location);

            await foreach (var c in repo.ObjectRepository.GetAll<GitCommit>(new HashSet<GitId>()))
            {
                TestContext.WriteLine($"Commit {c.Id} - {GitTools.FirstLine(c.Message)}");
                if (c.Parent != null)
                    TestContext.WriteLine($" -parent {c.Parent?.Id} - {GitTools.FirstLine(c.Parent?.Message)}");
                TestContext.WriteLine($" -tree {c.Tree?.Id}");

                foreach (var v in c.Tree!)
                {
                    TestContext.WriteLine($"   - {v.Name}");
                }
            }
        }

        [TestMethod]
        public async Task WalkCommitsAsync()
        {
            using var repo = GitRepository.Open(typeof(GitRepositoryTests).Assembly.Location);

            await foreach (var c in repo.Commits)
            {
                TestContext.WriteLine($"Commit {c.Id:x10} - {GitTools.FirstLine(c.Message)}");
                TestContext.WriteLine($"Author: {c.Author?.ToString() ?? "-"}");
                if (c.Parent != null)
                    TestContext.WriteLine($" -parent {c.Parent?.Id} - {GitTools.FirstLine(c.Parent?.Message)}");
                TestContext.WriteLine($" -tree {c.Tree?.Id}");

                foreach (var v in c.Tree)
                {
                    TestContext.WriteLine($"   - {v.Name}");
                }
            }
        }

        [TestMethod]
        public void WalkCommits()
        {
            using var repo = GitRepository.Open(typeof(GitRepositoryTests).Assembly.Location);

            foreach (var c in repo.Commits)
            {
                TestContext.WriteLine($"Commit {c.Id:x8} - {GitTools.FirstLine(c.Message)}");
                if (c.Parent != null)
                    TestContext.WriteLine($" -parent {c.Parent?.Id} - {GitTools.FirstLine(c.Parent?.Message)}");
                TestContext.WriteLine($" -tree {c.Tree?.Id}");

                foreach (var v in c.Tree!)
                {
                    TestContext.WriteLine($"   - {v.Name}");
                }
            }
        }

        [TestMethod]
        public async Task WalkRefLogHead()
        {
            using var repo = GitRepository.Open(typeof(GitRepositoryTests).Assembly.Location);
            int n = 0;

            await foreach (var c in repo.Head.ReferenceChanges)
            {
                TestContext.WriteLine($"{c.OriginalId} {c.TargetId} {c.Signature}\t{c.Summary}");
                Assert.IsNotNull(c.OriginalId);
                Assert.IsNotNull(c.TargetId);
                Assert.IsNotNull(c.Signature);
                Assert.IsNotNull(c.Summary);
                n++;
            }

            Assert.IsTrue(repo.Head.ReferenceChanges.Last().Signature.When >= repo.Head.Commit!.Committer!.When);

            Assert.IsTrue(n > 0);
        }

        [TestMethod]
        public async Task WalkRefLogMaster()
        {
            using var repo = GitRepository.Open(typeof(GitRepositoryTests).Assembly.Location);
            int n = 0;

            await foreach (var c in repo.Head.Resolved.ReferenceChanges)
            {
                TestContext.WriteLine($"{c.OriginalId} {c.TargetId} {c.Signature}\t{c.Summary}");
                Assert.IsNotNull(c.OriginalId);
                Assert.IsNotNull(c.TargetId);
                Assert.IsNotNull(c.Signature);
                Assert.IsNotNull(c.Summary);
                n++;
            }

            Assert.IsTrue(repo.Head.Resolved.ReferenceChanges.Last().Signature.When >= repo.Head.Commit!.Committer!.When);

            Assert.IsTrue(n > 0);
        }

        [TestMethod]
        public void WalkOne()
        {
            using var repo = GitRepository.Open(typeof(GitRepositoryTests).Assembly.Location);

            var treeId = repo.Head.Commit!.Tree.Id.ToString();

            var tree = repo.Trees.First(x => x.Id.ToString() == treeId);

            foreach (var v in tree)
            {
                TestContext.WriteLine($"   - {v.Name} - {v.Id}");
            }

            foreach (var v in tree.AllItems)
            {
                TestContext.WriteLine($"# {v.Path}");
            }
        }

        public void WalkSets_TestSet<TSet, TProp>(IQueryable<TProp> set, PropertyInfo pi, HashSet<Type> walked)
        {
            try
            {
                foreach (var v in set)
                {
                    break;
                }
            }
            catch (Exception e)
            {
                throw new AssertFailedException($"foreach on {pi.Name} works", e);
            }

            try
            {
                IEnumerable<TProp> set2 = set;

                set2.Any();
            }
            catch (Exception e)
            {
                throw new AssertFailedException($"enumerable Any on {typeof(TSet).Name}.{pi.Name} works", e);
            }

            try
            {
                set.Any();
            }
            catch (Exception e)
            {
                throw new AssertFailedException($"queryable Any on {typeof(TSet).Name}.{pi.Name} works", e);
            }

            if (set is IAsyncEnumerable<TProp> ae)
            {
                try
                {
                    ae.AnyAsync().AsTask().GetAwaiter().GetResult();
                }
                catch (Exception e)
                {
                    throw new AssertFailedException($"async enumerable AnyAsync on {typeof(TSet).Name}.{pi.Name} works", e);
                }
            }

            if (set is Linq.AsyncQueryable.IAsyncQueryable<TProp> aq)
            {
                try
                {
                    aq.Any();
                }
                catch (Exception e)
                {
                    throw new AssertFailedException($"async queryable Any on {typeof(TSet).Name}.{pi.Name} works", e);
                }

                try
                {
                    aq.AnyAsync().AsTask().GetAwaiter().GetResult();
                }
                catch (Exception e)
                {
                    throw new AssertFailedException($"async queryable AnyAsync on {typeof(TSet).Name}.{pi.Name} works", e);
                }
            }

            try
            {
                foreach (var v in set.Where(x => true).Take(2))
                { }
            }
            catch (Exception e)
            {
                throw new AssertFailedException($"queryable check on {typeof(TSet).Name}.{pi.Name} works", e);
            }


            WalkSets_TestType(set, walked);
        }

        public void WalkSets_TestType<T>(T instance, HashSet<Type> walked)
        {
            if (walked.Contains(typeof(T)))
                return;

            walked.Add(typeof(T));

            foreach (var prop in typeof(T).GetProperties().Where(prop => typeof(IQueryable).IsAssignableFrom(prop.PropertyType) && !prop.GetIndexParameters().Any()))
            {
                IQueryable v = (IQueryable)prop.GetValue(instance)!;
                Assert.IsNotNull(v, $"{typeof(T).Name}.{prop.Name} is not null");

                try
                {
                    typeof(GitRepositoryTests).GetMethod("WalkSets_TestSet")!.MakeGenericMethod(typeof(T), v.ElementType).Invoke(this, new object[] { v, prop, walked });
                }
                catch (Exception e)
                {
                    throw new TargetInvocationException($"When trying the {typeof(T).Name}.{prop.Name} property", e);
                }
            }

            foreach (var prop in typeof(T).GetProperties().Where(prop => !typeof(IQueryable).IsAssignableFrom(prop.PropertyType) && !prop.GetIndexParameters().Any()))
            {
                object? ob;
                try
                {
                    ob = prop.GetValue(instance)!;
                }
                catch (Exception e)
                {
                    throw new AssertFailedException($"Fetching {typeof(T).Name}.{prop.Name} works", e);
                }

                try
                {
                    if (ob != null)
                    {
                        typeof(GitRepositoryTests).GetMethod(nameof(WalkSets_TestType))!.MakeGenericMethod(prop.PropertyType).Invoke(this, new object[] { ob, walked });
                    }
                }
                catch (Exception e)
                {
                    throw new TargetInvocationException($"When trying the {typeof(T).Name}.{prop.Name} property", e);
                }
            }
        }

        [TestMethod]
        public void WalkSetsEmptyRepository()
        {
            using var repo = GitRepository.Init(Path.Combine(TestContext.TestRunResultsDirectory, "Nothing"));
            HashSet<Type> walked = new HashSet<Type>();

            WalkSets_TestType(repo, walked);
        }

        [TestMethod]
        public void WalkSetsDevRepository()
        {
            using var repo = GitRepository.Open(typeof(GitRepositoryTests).Assembly.Location);
            HashSet<Type> walked = new HashSet<Type>();

            WalkSets_TestType(repo, walked);
        }


        class RepoItem
        {
            public string Name { get; set; } = default!;
            public string? Content { get; set; }
        }

        [TestMethod]
        public async Task CreateRawObjects()
        {
            using var repo = GitRepository.Init(Path.Combine(TestContext.TestRunResultsDirectory, "RawObjectTest"));

            GitBlobWriter bw = GitBlobWriter.CreateFrom(System.Text.Encoding.UTF8.GetBytes("This is 'iota'.").AsBucket());

            var blob = await bw.WriteAndFetchAsync(repo);

            Assert.IsNotNull(blob);
            Assert.AreEqual("ce5beb5e8714fe6d04096988a48589e8312451a8", blob.Id.ToString());


            GitTreeWriter tw = GitTreeWriter.CreateEmpty();

            tw.Add("iota", blob);

            var tree = await tw.WriteAndFetchAsync(repo);

            Assert.AreEqual("f6315a2112111a87d565eef0175d25ed01c7da6e", tree.Id.ToString());

            var fsckOutput = await repo.GetPlumbing().ConsistencyCheck(new GitConsistencyCheckArgs() { Full = true });
            Assert.AreEqual($"dangling tree f6315a2112111a87d565eef0175d25ed01c7da6e", fsckOutput);
        }

        [TestMethod]
        public async Task CreateSvnTree()
        {
            using var repo = GitRepository.Init(Path.Combine(TestContext.TestRunResultsDirectory, TestContext.TestName));

            var items = new RepoItem[]
            {
                new RepoItem { Name = "iota", Content="This is dthe file 'iota'.\n" },
                new RepoItem { Name = "A" },
                new RepoItem { Name = "A/mu", Content="This is the file 'mu'.\n" },
                new RepoItem { Name = "A/B" },
                new RepoItem { Name = "A/B/lambda", Content="This is the file 'lambda'.\n"},
                new RepoItem { Name = "A/B/E", },
                new RepoItem { Name = "A/B/E/alpha", Content="This is the file 'alpha'.\n"},
                new RepoItem { Name = "A/B/E/beta", Content="This is the file 'beta'.\n" },
                new RepoItem { Name = "A/B/F" },
                new RepoItem { Name = "A/C" },
                new RepoItem { Name = "A/D" },
                new RepoItem { Name = "A/D/gamma", Content="This is the file 'gamma'.\n" },
                new RepoItem { Name = "A/D/G" },
                new RepoItem { Name = "A/D/G/pi", Content="This is the file 'pi'.\n" },
                new RepoItem { Name = "A/D/G/rho", Content="This is the file 'rho'.\n" },
                new RepoItem { Name = "A/D/G/tau", Content="This is the file 'tau'.\n" },
                new RepoItem { Name = "A/D/H" },
                new RepoItem { Name = "A/D/H/chi", Content = "This is the file 'chi'.\n" },
                new RepoItem { Name = "A/D/H/psi", Content = "This is the file 'psi'.\n" },
                new RepoItem { Name = "A/D/H/omega", Content = "This is the file 'omega'.\n" }
            };

            Hashtable ht = new Hashtable();
            foreach (var i in items.Where(x => x.Content != null))
            {
                GitBlobWriter b = GitBlobWriter.CreateFrom(System.Text.Encoding.UTF8.GetBytes(i.Content!).AsBucket());

                var r = await b.WriteAndFetchAsync(repo);
                ht[i.Name] = r;
            }

            foreach (var m in items.Where(x => x.Content == null).OrderByDescending(x => x.Name).Concat(new[] { new RepoItem { Name = "" } }))
            {
                GitTreeWriter t = GitTreeWriter.CreateEmpty();

                foreach (var o in items.Where(x => x.Name.StartsWith(m.Name + "/") || (m.Name == "" && !x.Name.Contains('/'))).Select(x => new { Item = x, Name = x.Name.Substring(m.Name.Length).TrimStart('/') }).Where(x => !x.Name.Contains('/')))
                {
                    if (o.Item.Content is not null)
                    {
                        var b = (GitBlob)ht[o.Item.Name]!;

                        t.Add(o.Name, b!);
                    }
                    else
                    {
                        var to = (GitTree)ht[o.Item.Name]!;

                        t.Add(o.Name, to!);
                    }
                }

                var r = await t.WriteAndFetchAsync(repo);
                ht[m.Name] = r;
            }

            GitCommitWriter cw = GitCommitWriter.Create(new GitCommit[0]);
            cw.Tree = ((GitTree)ht[""]).AsWriter();

            cw.Author = cw.Committer = new GitSignature("BH", "bh@BH", new DateTimeOffset(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Local)));

            var cs = await cw.WriteAndFetchAsync(repo);


            var fsckOutput = await repo.GetPlumbing().ConsistencyCheck(new GitConsistencyCheckArgs() { Full = true });
            Assert.AreEqual($"dangling commit {cs.Id}", fsckOutput);

            Assert.AreEqual(items.Length + 1, repo.Objects.Count()); // F and C are the same empty tree
            Assert.IsFalse(items.Select(x => x.Name).Except(((IEnumerable<GitTreeItem>)repo.Commits[cs.Id]!.Tree.AllItems).Select(x => x.Path)).Any(), "All paths reached");
        }

        [TestMethod]
        public async Task CreateSvnTree2()
        {
            using var repo = GitRepository.Init(Path.Combine(TestContext.TestRunResultsDirectory, TestContext.TestName));

            GitCommitWriter cw = GitCommitWriter.Create(new GitCommitWriter[0]);

            var items = new RepoItem[]
            {
                new RepoItem { Name = "iota", Content="This is dthe file 'iota'.\n" },
                new RepoItem { Name = "A" },
                new RepoItem { Name = "A/mu", Content="This is the file 'mu'.\n" },
                new RepoItem { Name = "A/B" },
                new RepoItem { Name = "A/B/lambda", Content="This is the file 'lambda'.\n"},
                new RepoItem { Name = "A/B/E", },
                new RepoItem { Name = "A/B/E/alpha", Content="This is the file 'alpha'.\n"},
                new RepoItem { Name = "A/B/E/beta", Content="This is the file 'beta'.\n" },
                new RepoItem { Name = "A/B/F" },
                new RepoItem { Name = "A/C" },
                new RepoItem { Name = "A/D" },
                new RepoItem { Name = "A/D/gamma", Content="This is the file 'gamma'.\n" },
                new RepoItem { Name = "A/D/G" },
                new RepoItem { Name = "A/D/G/pi", Content="This is the file 'pi'.\n" },
                new RepoItem { Name = "A/D/G/rho", Content="This is the file 'rho'.\n" },
                new RepoItem { Name = "A/D/G/tau", Content="This is the file 'tau'.\n" },
                new RepoItem { Name = "A/D/H" },
                new RepoItem { Name = "A/D/H/chi", Content = "This is the file 'chi'.\n" },
                new RepoItem { Name = "A/D/H/psi", Content = "This is the file 'psi'.\n" },
                new RepoItem { Name = "A/D/H/omega", Content = "This is the file 'omega'.\n" }
            };

            foreach (var item in items)
            {
                if (item.Content is not null)
                {
                    cw.Tree.Add(item.Name, GitBlobWriter.CreateFrom(System.Text.Encoding.UTF8.GetBytes(item.Content!).AsBucket()));
                }
                else
                {
                    cw.Tree.Add(item.Name, GitTreeWriter.CreateEmpty());
                }
            }

            cw.Author = cw.Committer = new GitSignature("BH", "bh@BH", new DateTimeOffset(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Local)));

            var cs = await cw.WriteAndFetchAsync(repo);

            var fsckOutput = await repo.GetPlumbing().ConsistencyCheck(new GitConsistencyCheckArgs() { Full = true });
            Assert.AreEqual($"dangling commit {cs.Id}", fsckOutput);

            Assert.AreEqual(items.Length + 1, repo.Objects.Count()); // F and C are the same empty tree
            Assert.IsFalse(items.Select(x => x.Name).Except(((IEnumerable<GitTreeItem>)repo.Commits[cs.Id]!.Tree.AllItems).Select(x => x.Path)).Any(), "All paths reached");

            cw = GitCommitWriter.Create(cs);

            cw.Author = cw.Committer = new GitSignature("BH", "bh@BH", new DateTimeOffset(new DateTime(2000, 1, 1, 1, 0, 0, DateTimeKind.Local)));

            cs = await cw.WriteAndFetchAsync(repo);

            fsckOutput = await repo.GetPlumbing().ConsistencyCheck(new GitConsistencyCheckArgs() { Full = true });
            Assert.AreEqual($"dangling commit {cs.Id}", fsckOutput);

            Assert.AreEqual(items.Length + 2, repo.Objects.Count()); // F and C are the same empty tree
            Assert.IsFalse(items.Select(x => x.Name).Except(((IEnumerable<GitTreeItem>)repo.Commits[cs.Id]!.Tree.AllItems).Select(x => x.Path)).Any(), "All paths reached");

            string? refName = ((GitSymbolicReference)repo.Head).ReferenceName;
            Assert.AreEqual("refs/heads/master", refName);
            await repo.GetPlumbing().UpdateReference(
                new GitUpdateReference { Name = refName!, Target = cs.Id }, 
                new GitUpdateReferenceArgs { Message = "Testing" });

            fsckOutput = await repo.GetPlumbing().ConsistencyCheck(new GitConsistencyCheckArgs() { Full = true });
            Assert.AreEqual($"", fsckOutput);
        }


    }
}
