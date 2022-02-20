using System;
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
            : base(repository)
        {
            if (!Directory.Exists(Path.Combine(objectsDir)))
                throw new GitRepositoryException($"{objectsDir} does not exist");

            ObjectsDir = objectsDir;
            _idType = GitIdType.Sha1;

            _repositories = new Lazy<GitObjectRepository[]>(() => GetRepositories().ToArray());            
        }

        Lazy<GitObjectRepository[]> _repositories;

        private IEnumerable<GitObjectRepository> GetRepositories()
        {
            int format = Repository.Configuration.GetInt("core", "repositoryformatversion") ?? -1;
            if (format == 1)
            {
                foreach (var (key, value) in Repository.Configuration.GetGroup("extensions", null))
                {
                    switch (key.ToLowerInvariant())
                    {
                        case "nop":
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

            // TODO: Check for multipack
            // TODO: Check for commitgraph

            foreach(var pack in Directory.GetFiles(Path.Combine(ObjectsDir, "pack"), "pack-*.pack"))
            {
                // TODO: Check if length matches hashtype?
                yield return new PackObjectRepository(Repository, pack, _idType);
            }

            yield return new FileObjectRepository(Repository, ObjectsDir);

            var alternatesFile = Path.Combine(ObjectsDir, "info/alternates");
            if (File.Exists(alternatesFile))
            {
                foreach(var line in File.ReadAllLines(alternatesFile))
                {
                    var l = line.Trim();
                    if (string.IsNullOrWhiteSpace(l))
                        continue;
                    else if (l[0] == '#' || l[0] == ';')
                        continue;

                    string? dir = null;
                    try
                    {
                        var p = Path.Combine(ObjectsDir, l);

                        if (Directory.Exists(p))
                            dir = p;
                    }
                    catch { }

                    if (dir != null)
                        yield return new GitRepositoryObjectRepository(Repository, dir);
                }
            }
        }

        public override async IAsyncEnumerable<TGitObject> GetAll<TGitObject>()
            where TGitObject : class
        {
            HashSet<GitId> returned = new HashSet<GitId>();

            foreach (var p in Sources)
            {
                await foreach(var v in p.GetAll<TGitObject>())
                {
                    if (!returned.Contains(v.Id!))
                    {
                        yield return v;

                        returned.Add(v.Id!);
                    }
                }
            }
        }

        public override async ValueTask<TGitObject?> Get<TGitObject>(GitId oid)
            where TGitObject : class
        {
            if (oid == null)
                throw new ArgumentNullException(nameof(oid));

            foreach (var p in Sources)
            {
                var r = await p.Get<TGitObject>(oid);

                if (r != null)
                    return r;
            }

            return null;
        }

        internal override async ValueTask<GitObjectBucket?> ResolveByOid(GitId oid)
        {
            // TODO: Implement
            if (oid == null)
                throw new ArgumentNullException(nameof(oid));

            foreach (var p in Sources)
            {
                var r = await p.ResolveByOid(oid);

                if (r != null)
                    return r;
            }

            return null;
        }

        protected GitObjectRepository[] Sources => _repositories.Value;
    }
}
