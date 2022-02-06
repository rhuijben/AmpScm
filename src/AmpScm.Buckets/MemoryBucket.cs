using System;
using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;

namespace AmpScm.Buckets
{
    public class MemoryBucket : Bucket, IBucketNoClose
    {
        BucketBytes _data;
        int _offset;

        public MemoryBucket(byte[] data)
        {
            _data = data ?? Array.Empty<byte>();
        }

        public MemoryBucket(byte[] data, int start, int length)
        {
            _data = new ReadOnlyMemory<byte>(data, start, length);
        }

        public MemoryBucket(ReadOnlyMemory<byte> data)
        {
            _data = data;
        }

        public override BucketBytes Peek()
        {
            return _data.Slice(_offset);
        }

        public override ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            int canRead = Math.Min(requested, _data.Length - _offset);

            if (canRead == 0 && requested > 0)
                return BucketBytes.Eof;

            var r = _data.Slice(_offset, canRead);
            _offset += r.Length;

            return r;
        }

        public override ValueTask<long?> ReadRemainingBytesAsync()
        {
            return new ValueTask<long?>(_data.Length - _offset);
        }

        public override ValueTask<Bucket> DuplicateAsync(bool reset)
        {
#pragma warning disable CA2000 // Dispose objects before losing scope
            var mb = new MemoryBucket(_data.Memory);
#pragma warning restore CA2000 // Dispose objects before losing scope
            if (!reset)
                mb._offset = _offset;

            return new ValueTask<Bucket>(mb);
        }

        public override long? Position => _offset;

        public override bool CanReset => true;

        public override ValueTask ResetAsync()
        {
            _offset = 0;

            return new ValueTask();
        }

#pragma warning disable CA1033 // Interface methods should be callable by child types
        Bucket IBucketNoClose.NoClose()
#pragma warning restore CA1033 // Interface methods should be callable by child types
        {
            return this;
        }
    }
}
