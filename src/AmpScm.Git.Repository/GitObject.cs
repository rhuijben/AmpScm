using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Git;
using AmpScm.Git.Sets;

namespace AmpScm.Git
{
    [DebuggerDisplay("{Type} {Id.ToString(\"x12\"),nq}")]
    public abstract class GitObject : IEquatable<GitObject>
    {
        protected internal GitRepository Repository { get; }
        public GitId Id { get; }

        public abstract GitObjectType Type { get; }

        private protected GitObject(GitRepository repository, GitId id)
        {
            Repository = repository ?? throw new ArgumentNullException(nameof(repository));
            Id = id ?? throw new ArgumentNullException(nameof(id));
        }

        internal static GitObject FromBucket(GitRepository repository, GitObjectBucket rdr, Type type, GitId id)
        {
            switch (rdr.Type)
            {
                case GitObjectType.Commit:
                    return new GitCommit(repository, rdr, id);
                case GitObjectType.Tree:
                    return new GitTree(repository, rdr, id);
                case GitObjectType.Blob:
                    return new GitBlob(repository, rdr, id);
                case GitObjectType.Tag:
                    return new GitTag(repository, rdr, id);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public virtual ValueTask ReadAsync()
        {
            return default;
        }

        public bool Equals(GitObject? other)
        {
            return other?.Id.Equals(Id) ?? false;
        }

        public override bool Equals(object? obj)
        {
            return base.Equals(obj as GitObject);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public static bool operator == (GitObject? one, GitObject? other)
        {
            return one?.Equals(other) ?? (other is null);
        }

        public static bool operator !=(GitObject? one, GitObject? other)
        {
            return !(one?.Equals(other) ?? (other is null));
        }
    }
}
