using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Amp.Buckets.Git;
using Amp.Git.Implementation;
using Amp.Git.Repository;
using Amp.Git.Sets;

namespace Amp.Git
{
    [DebuggerDisplay("GitRepository {GitDir}")]
    public partial class GitRepository : IDisposable, IGitQueryRoot, IServiceProvider
    {
        readonly ServiceContainer _container;
        private bool disposedValue;
        public string? FullPath { get; }
        public bool IsBare { get; }
        public bool IsLazy => Configuration.Lazy.RepositoryIsLazy;
        readonly Lazy<GitConfiguration> _gitConfiguration;

        protected string GitDir { get; }

        // Not directly creatable for now
        private GitRepository()
        {
            _container = new ServiceContainer();

            SetQueryProvider = new GitQueryProvider(this);
            Objects = new GitSet<GitObject>(this, () => this.Objects!);
            Commits = new GitCommitSet(this, () => this.Commits!);
            Blobs = new GitSet<GitBlob>(this, () => this.Blobs!);
            Tags = new GitSet<GitTag>(this, () => this.Tags!);
            Trees = new GitSet<GitTree>(this, () => this.Trees!);
            References = new GitReferenceSet(this, () => this.References!);
            _gitConfiguration = new Lazy<GitConfiguration>(LoadConfig);

            ObjectRepository = null!;
            GitDir = null!;
        }

        internal GitRepository(string path, bool bare = false)
            : this()
        {
            FullPath = GitTools.GetNormalizedFullPath(path);

            // TODO: Needs config check
            if (bare && FullPath.EndsWith(Path.DirectorySeparatorChar + ".git"))
            {
                GitDir = FullPath;

                if (!(Configuration?.GetBool("core", "bare", false) ?? false))
                {
                    bare = false;
                    FullPath = Path.GetDirectoryName(FullPath) ?? throw new InvalidOperationException();
                }
            }

            IsBare = bare;

            if (!IsBare)
                GitDir = Path.Combine(FullPath, ".git");
            else
                GitDir = FullPath;

            ObjectRepository = new Objects.GitRepositoryObjectRepository(this, Path.Combine(GitDir, "objects"));
            ReferenceRepository = new References.GitRepositoryReferenceRepository(this, GitDir);
        }

        public GitSet<GitObject> Objects { get; }
        public GitCommitSet Commits { get; }
        public GitSet<GitTree> Trees { get; }
        public GitSet<GitBlob> Blobs { get; }

        public GitSet<GitTag> Tags { get; }
        public GitReferenceSet References { get; }

        public GitConfiguration Configuration => _gitConfiguration.Value;

        internal GitQueryProvider SetQueryProvider { get; }

        public Objects.GitObjectRepository ObjectRepository { get; }
        public References.GitReferenceRepository ReferenceRepository { get; }

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

        IQueryable<TResult> IGitQueryRoot.GetAll<TResult>()
            where TResult : class
        {
            return SetQueryProvider.GetAll<TResult>();
        }

        IQueryable<TResult> IGitQueryRoot.GetAllNamed<TResult>()
            where TResult : class
        {
            return SetQueryProvider.GetAllNamed<TResult>();
        }

        ValueTask<TResult?> IGitQueryRoot.GetAsync<TResult>(GitObjectId objectId)
            where TResult : class
        {
            return SetQueryProvider.GetAsync<TResult>(objectId);
        }

        ValueTask<TResult?> IGitQueryRoot.GetNamedAsync<TResult>(string name)
            where TResult : class
        {
            return SetQueryProvider.GetNamedAsync<TResult>(name);
        }

        internal ValueTask<TResult?> GetAsync<TResult>(GitObjectId id)
            where TResult : GitObject
        {
            return SetQueryProvider.GetAsync<TResult>(id);
        }

        object? IServiceProvider.GetService(Type serviceType)
        {
            return ((IServiceProvider)_container).GetService(serviceType);
        }

        internal T? GetService<T>()
            where T : class
        {
            return _container.GetService(typeof(T)) as T;
        }
    }
}
