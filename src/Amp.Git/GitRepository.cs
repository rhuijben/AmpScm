using System;
using System.IO;
using System.Linq;
using Amp.Buckets.Git;
using Amp.Git.Implementation;
using Amp.Git.Sets;

namespace Amp.Git
{
    public partial class GitRepository : IDisposable, IGitQueryRoot
    {
        private bool disposedValue;
        public string? FullPath { get; }
        public bool IsBare { get; }

        protected string GitDir { get; }

        // Not directly creatable for now
        private GitRepository()
        {
            SetQueryProvider = new GitQueryProvider(this);
            Objects = new GitSet<GitObject>(this, () => this.Objects!);
            Commits = new GitCommitSet(this, () => this.Commits!);
            Blobs = new GitSet<GitBlob>(this, () => this.Blobs!);
            Tags = new GitSet<GitTag>(this, () => this.Tags!);
            Trees = new GitSet<GitTree>(this, () => this.Trees!);

            ObjectRepository = null!;
        }

        internal GitRepository(string path, bool bare = false)
            : this()
        {
            FullPath = GitTools.GetNormalizedFullPath(path);

            // TODO: Needs config check
            if (bare && FullPath.EndsWith(Path.DirectorySeparatorChar + ".git"))
            {
                bare = false;
                FullPath = Path.GetDirectoryName(FullPath);
            }

            IsBare = bare;

            if (!IsBare)
                GitDir = Path.Combine(FullPath, ".git");
            else
                GitDir = FullPath;

            ObjectRepository = new Objects.GitRepositoryObjectRepository(this, Path.Combine(GitDir, "objects"));
        }

        public GitSet<GitObject> Objects { get; }
        public GitCommitSet Commits { get; }
        public GitSet<GitTree> Trees { get; }
        public GitSet<GitBlob> Blobs { get; }
        public GitSet<GitTag> Tags { get; }

        internal GitQueryProvider SetQueryProvider { get; }

        public Objects.GitObjectRepository ObjectRepository {get;}

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public IQueryable<TResult> GetAll<TResult>() where TResult : GitObject
        {
            return SetQueryProvider.GetAll<TResult>();
        }

        internal class GitInternalConfigAccess
        {
            public GitObjectIdType IdType => GitObjectIdType.Sha1;
            public int IdBytes => 20;
        }
        internal GitInternalConfigAccess InternalConfig { get; } = new GitInternalConfigAccess();
    }
}
