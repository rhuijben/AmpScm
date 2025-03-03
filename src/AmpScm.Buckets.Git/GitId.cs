﻿using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;

[assembly: CLSCompliant(true)]

namespace AmpScm.Git
{
    public enum GitIdType
    {
        None = 0,
        Sha1 = 1,
        Sha256 = 2,
    }

    [DebuggerDisplay("{Type}:{ToString(),nq}")]
    public sealed class GitId : IEquatable<GitId>, IComparable<GitId>, IFormattable
    {
        byte[] _bytes;
        int _offset;
        public GitIdType Type { get; }

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

        public GitId(GitIdType type, byte[] hash)
        {
            if (type < GitIdType.None || type > GitIdType.Sha256)
                throw new ArgumentOutOfRangeException(nameof(type));

            Type = type;
            _bytes = (type != GitIdType.None ? hash ?? throw new ArgumentNullException(nameof(hash)) : Array.Empty<byte>());
        }

        GitId(GitIdType type, byte[] hash, int offset)
        {
            Type = type;
            _bytes = hash;
            _offset = offset;

            if (offset + HashLength(type) > hash.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));
        }

        /// <summary>
        /// Creates GitId that uses a location inside an existing array.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="hash"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        /// <remarks>Only use this if you are 100% sure the source array doesn't change, as changing it will change the objectid
        /// and break things like equals and hashing</remarks>
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public static GitId FromByteArrayOffset(GitIdType type, byte[] hash, int offset)
        {
            if (hash is null)
                throw new ArgumentNullException(nameof(hash));

            return new GitId(type, hash, offset);
        }

        public override bool Equals(object? obj)
        {
            return base.Equals(obj as GitId);
        }

        public bool Equals(GitId? other)
        {
            if (other is null)
                return false;

            if (other.Type != Type)
                return false;

            return HashCompare(other) == 0;
        }

        public int HashCompare(GitId other)
        {
            if (other is null)
                return 1;

            int sz = HashLength(Type);

            for (int i = 0; i < sz; i++)
            {
                int n = _bytes[i + _offset] - other._bytes[i + other._offset];

                if (n != 0)
                    return n;
            }

            return 0;
        }

        public static bool TryParse(string idString, out GitId id)
        {
            if (idString is null)
                throw new ArgumentNullException(nameof(idString));

            if ((idString.Length & 0x3) != 0 && (char.IsWhiteSpace(idString, 0) || char.IsWhiteSpace(idString, idString.Length - 1)))
                idString = idString.Trim();

            if (idString.Length == 40)
            {
                id = new GitId(GitIdType.Sha1, StringToByteArray(idString));
                return true;
            }
            else if (idString.Length == 64)
            {
                id = new GitId(GitIdType.Sha256, StringToByteArray(idString));
                return true;
            }
            else
            {
                id = null!;
                return false;
            }
        }

        public static GitId Parse(string idString)
        {
            if (TryParse(idString, out var v))
                return v;
            else
                throw new ArgumentOutOfRangeException(nameof(idString));
        }

        public static byte[] StringToByteArray(string hex)
        {
            if (string.IsNullOrEmpty(hex))
                throw new ArgumentNullException(nameof(hex));

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

        const string hexChars = "0123456789abcdef";
        public override string ToString()
        {
            int byteCount = HashLength(Type);
            var chars = new char[byteCount * 2];

            for (int i = 0; i < byteCount; i++)
            {
                var b = _bytes[_offset + i];
                chars[2 * i] = hexChars[b >> 4];
                chars[2 * i + 1] = hexChars[b & 0xF];
            }

            return new string(chars);
        }

        /// <summary>
        /// Return length of hash in bytes
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static int HashLength(GitIdType type)
            => type switch
            {
                GitIdType.Sha1 => 20,
                GitIdType.Sha256 => 32,
                _ => throw new ArgumentOutOfRangeException(nameof(type))
            };

        public int CompareTo(GitId? other)
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
            if (format!.StartsWith("x", StringComparison.Ordinal) && int.TryParse(format.Substring(1), out var xLen))
                return ToString().Substring(0, xLen);
            else if (format.StartsWith("X", StringComparison.Ordinal) && int.TryParse(format.Substring(1), out var xxlen))
                return ToString().Substring(0, xxlen).ToUpperInvariant();

            throw new ArgumentOutOfRangeException(nameof(format));
        }

        public static bool operator ==(GitId? one, GitId? other)
            => one?.Equals(other) ?? (other is null);

        public static bool operator !=(GitId? one, GitId? other)
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

        public static bool operator <(GitId left, GitId right)
        {
            return (left is null) ? !(right is null) : left.CompareTo(right) < 0;
        }

        public static bool operator <=(GitId left, GitId right)
        {
            return (left is null) || left.CompareTo(right) <= 0;
        }

        public static bool operator >(GitId left, GitId right)
        {
            return !(left is null) && left.CompareTo(right) > 0;
        }

        public static bool operator >=(GitId left, GitId right)
        {
            return (left is null) ? (right is null) : left.CompareTo(right) >= 0;
        }
    }
}
