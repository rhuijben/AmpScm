using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace AmpScm.Git
{
    public enum GitObjectIdType
    {
        None = 0,
        Sha1 = 1,
        Sha256 = 2,
    }

    [DebuggerDisplay("{Type}:{ToString(),nq}")]
    public sealed class GitObjectId : IEquatable<GitObjectId>, IComparable<GitObjectId>, IFormattable
    {
        byte[] _bytes;
        int _offset;
        public GitObjectIdType Type { get; }

        public byte[] Hash
        {
            get => (_offset == 0 && _bytes.Length == HashLength(Type)) ? _bytes : CopyArray();
        }

        private byte[] CopyArray()
        {
            var newBytes = new byte[HashLength(Type)];
            Array.Copy(_bytes, _offset, newBytes, 0, newBytes.Length);
            _bytes = newBytes;
            _offset = 0;
            return _bytes;
        }

        public GitObjectId(GitObjectIdType type, byte[] hash)
        {
            if (type < GitObjectIdType.None || type > GitObjectIdType.Sha256)
                throw new ArgumentOutOfRangeException(nameof(type));

            Type = type;
            _bytes = (type != GitObjectIdType.None ? hash ?? throw new ArgumentNullException(nameof(hash)) : Array.Empty<byte>());
        }

        GitObjectId(GitObjectIdType type, byte[] hash, int offset)
        {
            Type = type;
            _bytes = hash;
            _offset = offset;

            if (offset + HashLength(type) > hash.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));
        }

        /// <summary>
        /// Creates GitObjectId that uses a location inside an existing array.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="hash"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        /// <remarks>Only use this if you are 100% sure the source array doesn't change, as changing it will change the objectid
        /// and break things like equals and hashing</remarks>
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public static GitObjectId FromByteArrayOffset(GitObjectIdType type, byte[] hash, int offset)
        {
            return new GitObjectId(type, hash, offset);
        }

        public override bool Equals(object? obj)
        {
            return base.Equals(obj as GitObjectId);
        }

        public bool Equals(GitObjectId? other)
        {
            if (other is null)
                return false;

            if (other.Type != Type)
                return false;

            return HashCompare(other) == 0;
        }

        public int HashCompare(GitObjectId other)
        {
            int sz = HashLength(Type);

            for (int i = 0; i < sz; i++)
            {
                int n = _bytes[i + _offset] - other._bytes[i + other._offset];

                if (n != 0)
                    return n;
            }

            return 0;
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
            int n = hex.Length / 2; // Note this trims an odd final hexdigit, if there is one
            byte[] bytes = new byte[n];

            for (int i = 0; i < n; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }

            return bytes;
        }


        public override int GetHashCode()
        {
            // Combination of First and some other should provide good hashing over subsets of hashes
            return BitConverter.ToInt32(_bytes, _offset) ^ BitConverter.ToInt32(_bytes, _offset + 16);
        }

        public override string ToString()
        {
            int byteCount = HashLength(Type);
            var sb = new StringBuilder(2 * byteCount);
            for (int i = 0; i < byteCount; i++)
                sb.Append(_bytes[_offset + i].ToString("x2"));

            return sb.ToString();
        }

        public static int HashLength(GitObjectIdType type)
            => type switch
            {
                GitObjectIdType.Sha1 => 20,
                GitObjectIdType.Sha256 => 32,
                _ => throw new ArgumentOutOfRangeException(nameof(type))
            };

        public int CompareTo(GitObjectId? other)
        {
            if (other is null)
                return 1;

            int n = (int)Type - (int)other.Type;
            if (n != 0)
                return n;

            return HashCompare(other);
        }

        string IFormattable.ToString(string? format, IFormatProvider? formatProvider)
        {
            return ToString(format);
        }

        public string ToString(string? format)
        {
            if (string.IsNullOrEmpty(format) || format == "G")
                return ToString();

            if (format == "x")
                return ToString().Substring(0, 8);
            else if (format == "X")
                return ToString().Substring(0, 8).ToUpperInvariant();
            if (format.StartsWith("x") && int.TryParse(format.Substring(1), out var xLen))
                return ToString().Substring(0, xLen);
            else if (format.StartsWith("X") && int.TryParse(format.Substring(1), out var xxlen))
                return ToString().Substring(0, xxlen).ToUpperInvariant();

            throw new ArgumentOutOfRangeException(nameof(format));
        }

        public static bool operator ==(GitObjectId? one, GitObjectId? other)
            => one?.Equals(other) ?? (other is null);

        public static bool operator !=(GitObjectId? one, GitObjectId? other)
            => !(one?.Equals(other) ?? (other is null));

        public byte this[int index]
        {
            get
            {
                if (index < 0 || index > HashLength(Type))
                    throw new ArgumentOutOfRangeException(nameof(index));

                return _bytes[index + _offset];
            }
        }
    }
}
