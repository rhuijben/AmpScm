﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.References
{
    internal class GitRepositoryReferenceRepository : GitReferenceRepository
    {
        public GitRepositoryReferenceRepository(GitRepository gitRepository, string gitDir)
            : base(gitRepository, gitDir)
        {
            _repositories = new Lazy<GitReferenceRepository[]>(() => GetRepositories().ToArray());
        }

        readonly Lazy<GitReferenceRepository[]> _repositories;

        private IEnumerable<GitReferenceRepository> GetRepositories()
        {
            yield return new GitFileReferenceRepository(this, GitDir);

            if (File.Exists(Path.Combine(GitDir, GitPackedRefsReferenceRepository.PackedRefsFile)))
                yield return new GitPackedRefsReferenceRepository(this, GitDir);

            //yield return new GitShellReferenceRepository(this, GitDir);
        }


        public override async IAsyncEnumerable<GitReference> GetAll()
        {
            var names = new HashSet<string>();

            foreach (var v in Sources)
            {
                await foreach (var r in v.GetAll())
                {
                    if (!names.Contains(r.Name))
                    {
                        names.Add(r.Name);
                        yield return r;
                    }
                }
            }
        }

        public override IAsyncEnumerable<GitReferenceChange> GetChanges(GitReference reference)
        {
            foreach (var v in Sources)
            {
                var r = v.GetChanges(reference);

                if (r is not null)
                    return r;
            }

            return AsyncEnumerable.Empty<GitReferenceChange>();
        }

        protected internal override async ValueTask<GitReference?> ResolveAsync(GitReference gitReference)
        {
            foreach (var v in Sources)
            {
                var r = await v.ResolveAsync(gitReference).ConfigureAwait(false);

                if (r is not null)
                    return r;
            }

            return null;
        }

        public override ValueTask<GitReference?> ResolveByOidAsync(GitId arg)
        {
            return base.ResolveByOidAsync(arg);
        }

        public GitSymbolicReference? HeadReference { get; private set; }
        protected internal override async ValueTask<GitReference?> GetUnsafeAsync(string name, bool findSymbolic)
        {
            if (findSymbolic && name == Head)
                return HeadReference ??= new GitSymbolicReference(this, Head);

            foreach (var v in Sources)
            {
                var r = await v.GetUnsafeAsync(name, findSymbolic).ConfigureAwait(false);

                if (r is not null)
                    return r;
            }

            return null;
        }

        protected GitReferenceRepository[] Sources => _repositories.Value;
    }
}
