using System;
using System.Buffers;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Amp.Buckets
{
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public struct BucketBytes : IEquatable<BucketBytes>, IValueOrEof<ReadOnlyMemory<byte>>
    {
        ReadOnlyMemory<byte> _data;
        bool _eof;

        public BucketBytes(ReadOnlyMemory<byte> data)
        {
            _data = data;
            _eof = false;
        }

        public BucketBytes(byte[] array, int start, int length)
        {
            if (array is null)
                throw new ArgumentNullException(nameof(array));

            _data = new ReadOnlyMemory<byte>(array, start, length);
            _eof = false;
        }

        private BucketBytes(bool eof)
        {
            _data = ReadOnlyMemory<byte>.Empty;
            _eof = eof;
        }

        public bool Equals(BucketBytes other)
        {
            return _data.Equals(other._data) && _eof == other._eof;
        }

        public override int GetHashCode()
        {
            return _data.GetHashCode();
        }

        public int Length => _data.Length;
        public bool IsEof => _eof;
        public bool IsEmpty => _data.IsEmpty;

        public ReadOnlySpan<byte> Span => _data.Span;

        public ReadOnlyMemory<byte> Memory => _data;

        public BucketBytes Slice(int start)
        {
            return new BucketBytes(_data.Slice(start));
        }

        public BucketBytes Slice(int start, int length)
        {
            return new BucketBytes(_data.Slice(start, length));
        }

        public byte[] ToArray()
        {
            return _data.ToArray();
        }

        public static implicit operator BucketBytes(ArraySegment<byte> segment)
        {
            return new BucketBytes(segment);
        }

        public static implicit operator BucketBytes(ReadOnlyMemory<byte> segment)
        {
            return new BucketBytes(segment);
        }

        public static implicit operator BucketBytes(byte[] array)
        {
            return new BucketBytes(array);
        }

        public static implicit operator ValueTask<BucketBytes>(BucketBytes v)
        {
            return new ValueTask<BucketBytes>(v);
        }

        public static readonly BucketBytes Empty = new BucketBytes(false);
        public static readonly BucketBytes Eof = new BucketBytes(true);


        public void CopyTo(Memory<byte> destination) => Span.CopyTo(destination.Span);

        public byte this[int index] => _data.Span[index];

        public MemoryHandle Pin()
        {
            return _data.Pin();
        }

        /// <summary>
        /// Copies the contents of the readonly-only memory into the destination. If the source
        /// and destination overlap, this method behaves as if the original values are in
        /// a temporary location before the destination is overwritten.
        ///
        /// <returns>If the destination is shorter than the source, this method
        /// return false and no data is written to the destination.</returns>
        /// </summary>
        /// <param name="destination">The span to copy items into.</param>
        public bool TryCopyTo(Memory<byte> destination) => Span.TryCopyTo(destination.Span);

        ReadOnlyMemory<byte> IValueOrEof<ReadOnlyMemory<byte>>.Value => _data;


        string DebuggerDisplay
        {
            get
            {
                if (IsEof)
                    return "<EOF>";
                else
                    return $"Length={Length}, Data='{System.Text.Encoding.ASCII.GetString(_data.Span)}'";
            }
        }

        #region ZLib optimization. Our ZLib doesn't use Span<> and Memory<> yet, but let's reuse byte[] directly instead of copying
        static Func<ReadOnlyMemory<byte>, (object, int)> MemoryExpander { get; } = FindExpander();

        static Func<ReadOnlyMemory<byte>, (object, int)> FindExpander()
        {
            ParameterExpression p = Expression.Parameter(typeof(ReadOnlyMemory<byte>), "x");

            var c = Expression.New(typeof((object, int)).GetConstructors().OrderByDescending(x => x.GetParameters().Length).First(),
                       Expression.Field(p, "_object"),
                       Expression.Field(p, "_index"));
            return Expression.Lambda<Func<ReadOnlyMemory<byte>, (object, int)>>(c, p).Compile();
        }

        internal (byte[]?, int) ExpandToArray()
        {
            if (_data.Length == 0)
                return (Array.Empty<byte>(), 0);

            var (ob, index) = MemoryExpander(_data);

            if (ob is byte[] arr)
                return (arr, index);

            byte[] data = ToArray();

            return (data, 0);
        }

        internal void Deconstruct(out byte[]? array, out int offset)
        {
            if (_data.Length == 0)
            {
                array = null;
                offset = 0;
                return;
            }

            object ob;
            (ob, offset) = MemoryExpander(_data);
            array = ob as byte[];
        }
        #endregion
    }

}
