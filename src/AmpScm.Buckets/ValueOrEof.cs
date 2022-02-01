using System.Diagnostics;

namespace AmpScm.Buckets
{
    interface IValueOrEof<T> where T : struct
    {
        public T Value { get; }
        public bool IsEof { get; }
    }

    [DebuggerDisplay("Value={Value}, Eof={IsEof}")]
    public struct ValueOrEof<T> : IValueOrEof<T> where T : struct
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

        public static implicit operator ValueOrEof<T>(T value) => new ValueOrEof<T>(value);
    }
}
