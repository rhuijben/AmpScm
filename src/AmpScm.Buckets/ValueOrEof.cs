using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace AmpScm.Buckets
{
    interface IValueOrEof<T> where T : struct
    {
        public T Value { get; }
        public bool IsEof { get; }
    }

    [DebuggerDisplay("Value={Value}, Eof={IsEof}")]
    public struct ValueOrEof<T> : IValueOrEof<T>, IEquatable<ValueOrEof<T>>
        where T : struct
    {
        T _value;
        bool _isEof;

        public ValueOrEof(T value)
        {
            _value = value;
            _isEof = false;
        }

        public ValueOrEof(bool eof)
        {
            _value = default;
            _isEof = eof;
        }

        public T Value => _value;

        public bool IsEof => _isEof;

#pragma warning disable CA2225 // Operator overloads have named alternates
        public static implicit operator ValueOrEof<T>(T value) => new ValueOrEof<T>(value);
#pragma warning restore CA2225 // Operator overloads have named alternates

        public bool Equals(ValueOrEof<T> other)
        {
            if (other._isEof != _isEof)
                return false;
            else
                return _value.Equals(other._value);
        }

        public override bool Equals(object? obj)
        {
            if (obj is ValueOrEof<T> v)
                return Equals(v);
            return false;
        }

        public override int GetHashCode()
        {
            return _value.GetHashCode() ^ (_isEof ? 77 : 0);
        }

        public static bool operator ==(ValueOrEof<T> left, ValueOrEof<T> right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ValueOrEof<T> left, ValueOrEof<T> right)
        {
            return !(left == right);
        }
    }
}
