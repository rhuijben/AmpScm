using System;
using System.Linq;

namespace Amp.Buckets.Git
{
    public enum GitObjectIdType
    {
        None = 0,
        Sha1 = 1,
        Sha256 = 2,
    }

    public class GitObjectId : IEquatable<GitObjectId>
    {
        public GitObjectIdType Type { get; }
        public byte[] Hash { get; }

        public GitObjectId(GitObjectIdType type, byte[] hash)
        {
            if (type < GitObjectIdType.None || type > GitObjectIdType.Sha256)
                throw new ArgumentOutOfRangeException(nameof(type));

            Hash = (type != GitObjectIdType.None ? hash ?? throw new ArgumentNullException(nameof(hash)) : Array.Empty<byte>());
        }

        public bool Equals(GitObjectId? other)
        {
            if (other is null)
                return false;

            return (other.Type == Type) && Hash.Length == other.Hash.Length && Hash.SequenceEqual(other.Hash);
        }
    }
}
