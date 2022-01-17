using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Amp.Buckets
{
    public class MemoryBucket : Bucket
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

        public override ValueTask<BucketBytes> PeekAsync(bool noPoll = false)
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

        public override long? Position => _offset;

        public override bool CanReset => true;

        public override ValueTask ResetAsync()
        {
            _offset = 0;

            return new ValueTask();
        }
    }
}
