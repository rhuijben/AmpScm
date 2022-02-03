using System;
using System.Diagnostics;
using System.Threading.Tasks;
using AmpScm.Buckets.Git;
using AmpScm.Buckets.Git.Objects;
using AmpScm.Git.Sets;

namespace AmpScm.Git
{
    [DebuggerDisplay("{EntryName} - {Id}")]
    public abstract class GitTreeEntry : IEquatable<GitTreeEntry>, IGitObject
    {
        internal GitTreeEntry(GitTree tree, string name, GitId objectId)
        {
            InTree = tree ?? throw new ArgumentNullException(nameof(tree));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Id = objectId ?? throw new ArgumentNullException(nameof(objectId));
        }

        protected GitTree InTree { get; }
        public string Name { get; }

        public virtual string EntryName => Name;

        public virtual GitTreeElementType TypeMask => GitTreeElementType.None;

        public override bool Equals(object? obj)
        {
            return base.Equals(obj as GitTreeEntry);
        }

        public bool Equals(GitTreeEntry? other)
        {
            return other?.Name == Name && (other?.InTree.Equals(InTree) ?? false);
        }

        public override int GetHashCode()
        {
            return InTree.GetHashCode() ^ Name.GetHashCode();
        }

        public abstract ValueTask ReadAsync();

        public abstract GitObject GitObject { get; }

        public GitId Id { get; }

        public static bool operator ==(GitTreeEntry e1, GitTreeEntry e2)
            => e1?.Equals(e2) ?? false;

        public static bool operator !=(GitTreeEntry e1, GitTreeEntry e2)
            => !(e1?.Equals(e2) ?? false);
    }

    public abstract class GitTreeEntry<TEntry, TObject> : GitTreeEntry
        where TEntry : GitTreeEntry<TEntry, TObject>
        where TObject : GitObject
    {
        bool _loaded;
        TObject? _object;


        internal GitTreeEntry(GitTree tree, string name, GitId item) : base(tree, name, item)
        {
        }

        public sealed override GitObject GitObject => Object;

        protected TObject Object
        {
            get
            {
                if (!_loaded)
                    ReadAsync().GetAwaiter().GetResult();

                return _object!;
            }
        }

        public override async ValueTask ReadAsync()
        {
            _object = await InTree.Repository.GetAsync<TObject>(Id);
            _loaded = true;
        }
    }

    public class GitFileTreeEntry : GitTreeEntry<GitFileTreeEntry, GitBlob>
    {
        internal GitFileTreeEntry(GitTree tree, string name, GitTreeElementType mask, GitId item) : base(tree, name, item)
        {
            TypeMask = mask;
        }

        public override GitTreeElementType TypeMask { get; }

        public GitBlob Blob => Object;

        public new GitBlob GitObject => Object;
    }

    public class GitDirectoryTreeEntry : GitTreeEntry<GitDirectoryTreeEntry, GitTree>
    {
        internal GitDirectoryTreeEntry(GitTree tree, string name, GitId item) : base(tree, name, item)
        {
        }

        public override string EntryName => Name + "/";

        public GitTree Tree => Object;

        public new GitTree GitObject => Object;

        public override GitTreeElementType TypeMask => GitTreeElementType.Directory;
    }
}
