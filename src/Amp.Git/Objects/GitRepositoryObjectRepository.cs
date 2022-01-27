using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amp.Buckets.Git;

namespace Amp.Git.Objects
{
    internal class GitRepositoryObjectRepository : GitObjectRepository
    {
        string ObjectsDir { get; }


        public GitRepositoryObjectRepository(GitRepository repository, string objectsDir)
            : base(repository)
        {
            if (!Directory.Exists(Path.Combine(objectsDir)))
                throw new GitRepositoryException($"{objectsDir} does not exist");

            ObjectsDir = objectsDir;
             _repositories = new Lazy<GitObjectRepository[]>(() => GetRepositories().ToArray());
        }


        Lazy<GitObjectRepository[]> _repositories;

        private IEnumerable<GitObjectRepository> GetRepositories()
        {
            // TODO: Check for multipack
            // TODO: Check for commitgraph

            foreach(var pack in Directory.GetFiles(Path.Combine(ObjectsDir, "pack"), "pack-*.pack"))
            {
                yield return new PackObjectRepository(Repository, pack);
            }

            yield return new FileObjectRepository(Repository, ObjectsDir);
        }

        public override async IAsyncEnumerable<TGitObject> GetAll<TGitObject>()
            where TGitObject : class
        {
            HashSet<GitObjectId> returned = new HashSet<GitObjectId>();

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

        public override async ValueTask<TGitObject?> Get<TGitObject>(GitObjectId objectId)
            where TGitObject : class
        {
            if (objectId == null)
                throw new ArgumentNullException(nameof(objectId));

            foreach (var p in Sources)
            {
                var r = await p.Get<TGitObject>(objectId);

                if (r != null)
                    return r;
            }

            return null;
        }

        protected GitObjectRepository[] Sources => _repositories.Value;
    }
}
