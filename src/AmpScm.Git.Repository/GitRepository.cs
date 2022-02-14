using System;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using AmpScm.Buckets.Git;
using AmpScm.Git.Implementation;
using AmpScm.Git.Repository;
using AmpScm.Git.Sets;

namespace AmpScm.Git
{
    [DebuggerDisplay("GitRepository {GitDir}")]
    public partial class GitRepository : IDisposable, IGitQueryRoot, IServiceProvider
    {
        readonly ServiceContainer _container;
        private bool disposedValue;
        public string FullPath { get; }
        public bool IsBare { get; }
        public bool IsLazy => Configuration.Lazy.RepositoryIsLazy;
        public bool IsShallow => Configuration.Lazy.RepositoryIsShallow;
        readonly Lazy<GitConfiguration> _gitConfiguration;

        internal protected string GitDir { get; }

        // Not directly creatable for now
        private GitRepository()
        {
            _container = new ServiceContainer();

            SetQueryProvider = new GitQueryProvider(this);
            Objects = new GitSet<GitObject>(this, () => this.Objects!);
            Commits = new GitCommitsSet(this, () => this.Commits!);
            Blobs = new GitSet<GitBlob>(this, () => this.Blobs!);
            TagObjects = new GitSet<GitTag>(this, () => this.TagObjects!);
            Trees = new GitSet<GitTree>(this, () => this.Trees!);
            References = new GitReferencesSet(this, () => this.References!);
            Remotes = new GitRemotesSet(this, () => this.Remotes!);
            _gitConfiguration = new Lazy<GitConfiguration>(LoadConfig);
            NoRevisions = new GitRevisionSet(this);

            ObjectRepository = null!;
            GitDir = null!;
            FullPath = null!;
            ReferenceRepository = null!;
        }

        internal GitRepository(string path, bool bareCheck = false)
            : this()
        {
            FullPath = GitTools.GetNormalizedFullPath(path);

            // TODO: Needs config check
            if (bareCheck && FullPath.EndsWith(Path.DirectorySeparatorChar + ".git"))
            {
                GitDir = FullPath;

                if (!(Configuration?.GetBool("core", "bare", false) ?? false))
                {
                    bareCheck = false;
                    FullPath = Path.GetDirectoryName(FullPath) ?? throw new InvalidOperationException();
                }
            }

            IsBare = bareCheck;

            if (!IsBare)
                GitDir = Path.Combine(FullPath, ".git");
            else
                GitDir = FullPath;

            ObjectRepository = new Objects.GitRepositoryObjectRepository(this, Path.Combine(GitDir, "objects"));
            ReferenceRepository = new References.GitRepositoryReferenceRepository(this, GitDir);
        }

        public GitSet<GitObject> Objects { get; }
        public GitCommitsSet Commits { get; }
        public GitSet<GitTree> Trees { get; }
        public GitSet<GitBlob> Blobs { get; }
        public GitSet<GitTag> TagObjects { get; }

        public GitReferencesSet References { get; }
        public GitRemotesSet Remotes { get; }

        internal GitRevisionSet NoRevisions { get; }

        public GitConfiguration Configuration => _gitConfiguration.Value;

        internal GitQueryProvider SetQueryProvider { get; }

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public Objects.GitObjectRepository ObjectRepository { get; }
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public References.GitReferenceRepository ReferenceRepository { get; }

        public GitReference Head => References.Head;

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

        ValueTask<TResult?> IGitQueryRoot.GetAsync<TResult>(GitId objectId)
            where TResult : class
        {
            return SetQueryProvider.GetAsync<TResult>(objectId);
        }

        ValueTask<TResult?> IGitQueryRoot.GetNamedAsync<TResult>(string name)
            where TResult : class
        {
            return SetQueryProvider.GetNamedAsync<TResult>(name);
        }

        internal ValueTask<TResult?> GetAsync<TResult>(GitId id)
            where TResult : GitObject
        {
            return SetQueryProvider.GetAsync<TResult>(id);
        }

        IQueryable<GitRevision> IGitQueryRoot.GetRevisions(GitRevisionSet p)
        {
            return SetQueryProvider.GetRevisions(p);
        }

        object? IServiceProvider.GetService(Type serviceType)
        {
            return ((IServiceProvider)_container).GetService(serviceType);
        }

        public override string ToString()
        {
            if (IsBare)
                return $"[Bare Repository] GitDir={GitDir}";
            else
                return $"[Git Repository] FullPath={FullPath}";
        }

        internal T? GetService<T>()
            where T : class
        {
            return _container.GetService(typeof(T)) as T;
        }        
    }
}
