﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Git;

namespace AmpScm.Git.Objects
{
    internal class GitRepositoryObjectRepository : GitObjectRepository
    {
        public string ObjectsDir { get; }
        public string? PromisorRemote { get; private set; }
        public GitIdType _idType;


        public GitRepositoryObjectRepository(GitRepository repository, string objectsDir)
            : base(repository, "Repository:" + Path.GetDirectoryName(objectsDir))
        {
            if (!Directory.Exists(Path.Combine(objectsDir)))
                throw new GitRepositoryException($"{objectsDir} does not exist");

            ObjectsDir = objectsDir;
            _idType = GitIdType.Sha1;

            _repositories = new Lazy<GitObjectRepository[]>(() => GetRepositories().ToArray());
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                {
                    if (_repositories.IsValueCreated)
                    {
                        foreach(var v in Sources)
                        {
                            v.Dispose();
                        }
                    }
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        Lazy<GitObjectRepository[]> _repositories;

        private IEnumerable<GitObjectRepository> GetRepositories()
        {
            int format = Repository.Configuration.GetInt("core", "repositoryformatversion") ?? -1;
            if (format == 1)
            {
                foreach (var (key, value) in Repository.Configuration.GetGroup("extensions", null))
                {
#pragma warning disable CA1308 // Normalize strings to uppercase
                    switch (key.ToLowerInvariant())
#pragma warning restore CA1308 // Normalize strings to uppercase
                    {
                        case "noop":
                            break;
                        case "partialclone":
                            PromisorRemote = value;
                            break;
                        case "objectformat":
                            if (string.Equals(value, "sha1", StringComparison.OrdinalIgnoreCase))
                            {
                                /* Do nothing */
                            }
                            else if (string.Equals(value, "sha256", StringComparison.OrdinalIgnoreCase))
                            {
                                Repository.SetSHA256(); // Ugly experimental hack for now
                                _idType = GitIdType.Sha256;
                            }
                            else
                                throw new GitException($"Found unsupported objectFormat {value} in repository {Repository.FullPath}");
                            break;
#if DEBUG
                        case "worktreeconfig":
                            break;
#endif
                        default:
                            throw new GitException($"Found unsupported extension {key} in repository {Repository.FullPath}");
                    }
                }
            }
            else if (format != 0)
            {
                throw new GitException($"Found unsupported repository format {format} for {Repository.FullPath}");
            }

            // Check for commit chain first, to allow cheap access to commit type
            string chain = Path.Combine(ObjectsDir, "info", "commit-graph");
            if (File.Exists(chain) && Repository.Configuration.Lazy.CommitGraph)
            {
                yield return new CommitGraphRepository(Repository, chain);
            }
            else if (Directory.Exists(chain += "s") && File.Exists(Path.Combine(chain, "commit-graph-chain")) && Repository.Configuration.Lazy.CommitGraph)
            {
                yield return new CommitGraphChainRepository(Repository, chain);
            }

            string multipackFile = Path.Combine(ObjectsDir, "pack", "multi-pack-index");
            MultiPackObjectRepository? multiPack = null;
            if (File.Exists(multipackFile) && Repository.Configuration.Lazy.MultiPack)
            {
                MultiPackObjectRepository mp = new MultiPackObjectRepository(Repository, multipackFile);

                if (mp.CanLoad())
                {
                    yield return mp;

                    multiPack = mp;
                }
                else
                    mp.Dispose();
            }

            foreach (var pack in Directory.GetFiles(Path.Combine(ObjectsDir, "pack"), "pack-*.pack"))
            {
                // TODO: Check if length matches hashtype?
                if (!multiPack?.ContainsPack(pack) ?? true)
                    yield return new PackObjectRepository(Repository, pack, _idType);
            }

            yield return new FileObjectRepository(Repository, ObjectsDir);

            var alternatesFile = Path.Combine(ObjectsDir, "info/alternates");
            if (File.Exists(alternatesFile))
            {
                foreach (var line in File.ReadAllLines(alternatesFile))
                {
                    var l = line.Trim();
                    if (string.IsNullOrWhiteSpace(l))
                        continue;
                    else if (l[0] == '#' || l[0] == ';')
                        continue;

                    string? dir = null;

                    var p = Path.Combine(ObjectsDir, l);

                    if (Directory.Exists(p))
                        dir = p;

                    if (dir != null)
                        yield return new GitRepositoryObjectRepository(Repository, dir);
                }
            }
        }

        public override async IAsyncEnumerable<TGitObject> GetAll<TGitObject>(HashSet<GitId> alreadyReturned)
            where TGitObject : class
        {
            HashSet<GitId> returned = new HashSet<GitId>();

            if (typeof(TGitObject) == typeof(GitTagObject))
            {
                // Tag is such an uncommon object that finding it globally is very
                // expensive, while the most common usecase is testsuites.
                //
                // Let's walk references of type tag first, as there should
                // be a reference pointing towards them anyway

                await foreach (var v in Repository.References.Where(x => x.IsTag))
                {
                    if (v.GitObject is GitTagObject tag && v.Id is not null && !returned.Contains(v.Id))
                    {
                        yield return (TGitObject)(object)tag;

                        returned.Add(tag.Id);
                    }
                }
            }

            foreach (var p in Sources)
            {
                await foreach (var v in p.GetAll<TGitObject>(returned))
                {
                    yield return v;
                    returned.Add(v.Id);
                }
            }
        }

        internal override async ValueTask<(T? Result, bool Success)> DoResolveIdString<T>(string idString, GitId baseGitId)
            where T : class
        {
            T? first = null;
            foreach (var p in Sources)
            {
                if (p.ProvidesGetObject)
                {
                    var (Result, Success) = await p.DoResolveIdString<T>(idString, baseGitId).ConfigureAwait(false);

                    if (!Success)
                        return (null, false);
                    else if (first is not null && Result is not null && Result.Id != first.Id)
                        return (null, false);
                    else if (first is null && Result is not null)
                        first = Result;
                }
            }

            return (first, true);
        }

        public override async ValueTask<TGitObject?> GetByIdAsync<TGitObject>(GitId id)
            where TGitObject : class
        {
            if (id is null)
                throw new ArgumentNullException(nameof(id));

            foreach (var p in Sources)
            {
                if (p.ProvidesGetObject)
                {
                    var r = await p.GetByIdAsync<TGitObject>(id).ConfigureAwait(false);

                    if (r != null)
                        return r;
                }
            }

            return null;
        }

        internal override async ValueTask<GitObjectBucket?> ResolveByOid(GitId id)
        {
            if (id is null)
                throw new ArgumentNullException(nameof(id));

            foreach (var p in Sources)
            {
                var r = await p.ResolveByOid(id).ConfigureAwait(false);

                if (r != null)
                    return r;
            }

            return null;
        }

        internal override async ValueTask<IGitCommitGraphInfo?> GetCommitInfo(GitId id)
        {
            if (id is null)
                throw new ArgumentNullException(nameof(id));

            foreach (var p in Sources)
            {
                if (p.ProvidesCommitInfo)
                {
                    var r = await p.GetCommitInfo(id).ConfigureAwait(false);

                    if (r != null)
                        return r;
                }
            }

            return null;
        }

        internal override bool ContainsId(GitId id)
        {
            if (id is null)
                throw new ArgumentNullException(nameof(id));

            foreach (var p in Sources)
            {
                if (p.ProvidesGetObject && p.ContainsId(id))
                    return true;
            }
            return false;
        }

        protected GitObjectRepository[] Sources => _repositories.Value;
    }
}
