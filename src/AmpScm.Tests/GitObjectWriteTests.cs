﻿using System;
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
    public class GitObjectWriteTests
    {
        public TestContext TestContext { get; set; } = null!;

        [TestMethod]
        public async Task CreateRawObjects()
        {
            using var repo = GitRepository.Init(TestContext.PerTestDirectory());

            GitBlobWriter bw = GitBlobWriter.CreateFrom(System.Text.Encoding.UTF8.GetBytes("This is 'iota'.").AsBucket());

            var blob = await bw.WriteAndFetchAsync(repo);

            Assert.IsNotNull(blob);
            Assert.AreEqual("ce5beb5e8714fe6d04096988a48589e8312451a8", blob.Id.ToString());


            GitTreeWriter tw = GitTreeWriter.CreateEmpty();

            tw.Add("iota", blob);

            var treeId = await tw.WriteToAsync(repo);

            Assert.AreEqual("f6315a2112111a87d565eef0175d25ed01c7da6e", treeId.ToString());

            var fsckOutput = await repo.GetPlumbing().ConsistencyCheck(new GitConsistencyCheckArgs() { Full = true });
            Assert.AreEqual($"dangling tree {treeId}", fsckOutput);
        }

        class RepoItem
        {
            public string Name { get; set; } = default!;
            public string? Content { get; set; }
        }

        [TestMethod]
        public async Task CreateSvnTree()
        {
            using var repo = GitRepository.Init(TestContext.PerTestDirectory());

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
            using var repo = GitRepository.Init(TestContext.PerTestDirectory());

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
