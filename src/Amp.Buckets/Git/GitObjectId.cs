using System;
using System.Linq;
using System.Text;

namespace Amp.Buckets.Git
{
    public enum GitObjectIdType
    {
        None = 0,
        Sha1 = 1,
        Sha256 = 2,
    }

    public sealed class GitObjectId : IEquatable<GitObjectId>
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

        public static bool TryParse(string s, out GitObjectId oid)
        {
            if (s.Length == 40)
            {
                oid = new GitObjectId(GitObjectIdType.Sha1, StringToByteArray(s));
                return true;
            }
            else if (s.Length == 64)
            {
                oid = new GitObjectId(GitObjectIdType.Sha256, StringToByteArray(s));
                return true;
            }
            else
            {
                oid = null!;
                return false;
            }
        }

        public static byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length / 2)
                             .Select(x => Convert.ToByte(hex.Substring(x * 2, 2), 16))
                             .ToArray();
        }


        public override int GetHashCode()
        {
            // Combination of First and last bytes should provide good hashing over subsets of hashes
            return BitConverter.ToInt32(Hash, 0) ^ BitConverter.ToInt32(Hash, Hash.Length-4);
        }

        public override string ToString()
        {
            var sb = new StringBuilder(2*Hash.Length);
            foreach (var b in Hash)
                sb.Append(b.ToString("x2"));

            return sb.ToString();
        }
    }
}
