using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Git
{
    public class GitDeltaBucket : GitBucket
    {
        protected Bucket BaseBucket { get; }
        long length;
        long position;
        readonly byte[] buffer = new byte[8];
        uint copy_offset;
        int copy_size;
        delta_state state;
        int p0;

        enum delta_state
        {
            start,
            init,
            src_copy,
            base_seek,
            base_copy,
            eof
        }

        public GitDeltaBucket(Bucket source, Bucket baseBucket)
            : base(source)
        {
            BaseBucket = baseBucket ?? throw new ArgumentNullException(nameof(baseBucket));
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing && !DontDisposeInner)
                {
                    BaseBucket.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        protected async override ValueTask DisposeAsyncCore()
        {
            if (!DontDisposeInner)
                await BaseBucket.DisposeAsync();

            await base.DisposeAsyncCore();
        }

        public override long? Position => position;

        static uint NumberOfSetBits(uint i)
        {
            i = i - ((i >> 1) & 0x55555555);
            i = (i & 0x33333333) + ((i >> 2) & 0x33333333);
            return (((i + (i >> 4)) & 0x0F0F0F0F) * 0x01010101) >> 24;
        }

        static int NeedBytes(byte data)
        {
            if (0 == (data & 0x80))
                return 1;
            else
                return (int)NumberOfSetBits(data);
        }

        async ValueTask<bool> AdvanceAsync()
        {
            while (state == delta_state.start)
            {
                while (p0 >= 0)
                {
                    // This initial loop re-uses length to collect the base size, as we don't have that
                    // value at this point anyway
                    var data = await Inner.ReadAsync(1);

                    if (data.IsEof)
                        throw new GitBucketException($"Unexpected EOF on Source {Inner.Name} Bucket");

                    byte uc = data[0];

                    int shift = (p0 * 7);
                    length |= (long)(uc & 0x7F) << shift;
                    p0++;

                    if (0 == (data[0] & 0x80))
                    {
                        long? base_size = await BaseBucket.ReadRemainingBytesAsync();

                        if (base_size != length)
                            throw new GitBucketException($"Expected delta base size {length} doesn't match source size ({base_size}) on {BaseBucket.Name} Bucket");

                        length = 0;
                        p0 = -1;
                    }
                }
                while (p0 < 0)
                {
                    var data = await Inner.ReadAsync(1);

                    if (data.IsEof)
                        throw new GitBucketException($"Unexpected EOF on Source {Inner.Name} Bucket");

                    byte uc = data[0];

                    int shift = ((-1 - p0) * 7);
                    length |= (long)(uc & 0x7F) << shift;
                    p0--;

                    if (0 == (data[0] & 0x80))
                    {
                        p0 = 0;
                        state = delta_state.init;
                    }
                }
            }

            while (state == delta_state.init)
            {
                BucketBytes data;
                if (p0 != 0)
                {
                    var want = NeedBytes(buffer[0]);

                    var read = await Inner.ReadAsync(want - p0);
                    if (read.IsEof)
                        return false;

                    for (int i = 0; i < read.Length; i++)
                        buffer[p0++] = read[i];

                    if (p0 < want)
                        continue;

                    data = new BucketBytes(buffer, 0, p0);
                    p0 = 0;
                }
                else
                {
                    int want;
                    bool peeked = false;

                    data = await Inner.PeekAsync();

                    if (!data.IsEmpty)
                    {
                        peeked = true;
                        want = NeedBytes(data[0]);
                    }
                    else
                        want = 1;

                    data = await Inner.ReadAsync(want);

                    if (data.IsEof)
                        throw new GitBucketException($"Unexpected EOF on Source {Inner.Name}");

                    if (!peeked)
                        want = NeedBytes(data[0]);

                    if (data.Length < want)
                    {
                        for (int i = 0; i < data.Length; i++)
                            buffer[i] = data[i];

                        p0 = data.Length;
                        continue;
                    }
                }

                byte uc = data[0];
                if (0 == (uc & 0x80))
                {
                    state = delta_state.src_copy;
                    copy_size = (uc & 0x7F);

                    if (copy_size == 0)
                        throw new GitBucketException("0 operation is reserved");
                }
                else
                {
                    copy_offset = 0;
                    copy_size = 0;

                    int i = 1;

                    if (0 != (uc & 0x01))
                        copy_offset |= (uint)data[i++] << 0;
                    if (0 != (uc & 0x02))
                        copy_offset |= (uint)data[i++] << 8;
                    if (0 != (uc & 0x04))
                        copy_offset |= (uint)data[i++] << 16;
                    if (0 != (uc & 0x08))
                        copy_offset |= (uint)data[i++] << 24;

                    if (0 != (uc & 0x10))
                        copy_size |= data[i++] << 0;
                    if (0 != (uc & 0x20))
                        copy_size |= data[i++] << 8;
                    if (0 != (uc & 0x40))
                        copy_size |= data[i++] << 16;

                    if (copy_size == 0)
                        copy_size = 0x10000;

                    if (copy_offset == BaseBucket.Position!.Value)
                        state = delta_state.base_copy;
                    else
                        state = delta_state.base_seek;
                }
            }
            return true;
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            if (requested <= 0)
                throw new ArgumentOutOfRangeException(nameof(requested));

            if (state <= delta_state.init && !await AdvanceAsync())
                return BucketBytes.Eof;

            Debug.Assert(state >= delta_state.src_copy && state <= delta_state.eof);

            if (state == delta_state.base_seek)
                await SeekBase();

            if (state == delta_state.base_copy)
            {
                var data = await BaseBucket.ReadAsync(Math.Min(requested, copy_size));

                if (data.IsEof)
                    throw new GitBucketException($"Unexpected EOF on Base {BaseBucket.Name} Bucket");

                position += data.Length;
                copy_size -= data.Length;

                if (copy_size == 0)
                {
                    if (position == length)
                        state = delta_state.eof;
                    else
                        state = delta_state.init;
                }
                return data;
            }
            else if (state == delta_state.src_copy)
            {
                var data = await Inner.ReadAsync(Math.Min(requested, copy_size));

                if (data.IsEof)
                    throw new GitBucketException($"Unexpected EOF on Source {Inner.Name} Bucket");

                position += data.Length;
                copy_size -= data.Length;

                if (copy_size == 0)
                {
                    if (position == length)
                        state = delta_state.eof;
                    else
                        state = delta_state.init;
                }
                return data;
            }
            else if (state == delta_state.eof)
            {
                var data = await Inner.ReadAsync(); // Ensure finish bytes of ZLib bucket are read

                if (!data.IsEof)
                    throw new InvalidOperationException($"Expected EOF on Source {Inner.Name} Bucket");

                return BucketBytes.Eof;
            }

            throw new InvalidOperationException();
        }

        public async override ValueTask<int> ReadSkipAsync(int requested)
        {
            int skipped = 0;
            while (requested > 0)
            {
                if (state <= delta_state.init && !await AdvanceAsync())
                    return skipped;

                Debug.Assert(state >= delta_state.src_copy && state <= delta_state.eof);

                int r;
                switch (state)
                {
                    case delta_state.base_seek:
                        // Avoid the seek and just do the calculations instead
                        r = Math.Min(requested, copy_size);
                        copy_size -= r;
                        copy_offset += (uint)r;
                        position += r;
                        break;
                    case delta_state.base_copy:
                        // Go back to a to-seek state
                        r = Math.Min(requested, copy_size);
                        copy_size -= r;
                        copy_offset = (uint)(BaseBucket.Position!.Value + r);
                        position += r;
                        state = delta_state.base_seek;
                        break;
                    case delta_state.src_copy:
                        // Just skip the source data
                        r = await Inner.ReadSkipAsync(Math.Min(requested, copy_size));
                        copy_size -= r;
                        position += r;
                        break;
                    case delta_state.eof:
                        return 0;
                    default:
                        throw new InvalidOperationException();
                }

                if (copy_size == 0)
                    state = delta_state.init;

                if (r == 0)
                    return skipped;

                skipped += r;
                requested -= r;
            }
            return skipped;
        }

        private async Task SeekBase()
        {
            while (state == delta_state.base_seek)
            {
                long cp = BaseBucket.Position!.Value;

                if (copy_offset < cp)
                {
                    await BaseBucket.ResetAsync();
                    cp = 0;
                }

                while (cp < copy_offset)
                {
                    long skipped = await BaseBucket.ReadSkipAsync(copy_offset - cp);
                    if (skipped == 0)
                        throw new InvalidOperationException($"Unexpected seek failure on {BaseBucket.Name} Bucket position {copy_offset} from {cp}");

                    cp += skipped;
                }

                if (cp == copy_offset)
                {
                    state = delta_state.base_copy;
                    copy_offset = 0;
                }
            }
        }

        public override async ValueTask<long?> ReadRemainingBytesAsync()
        {
            while (state < delta_state.init)
            {
                if (!await AdvanceAsync())
                    return null;
            }

            return (length - Position);
        }

        public override async ValueTask<BucketBytes> PeekAsync()
        {
            if (state == delta_state.base_copy)
            {
                var data = await BaseBucket.PeekAsync();

                if (copy_size < data.Length)
                    data = data.Slice(0, copy_size);

                return data;
            }
            else if (state == delta_state.src_copy)
            {
                var data = await Inner.PeekAsync();

                if (copy_size < data.Length)
                    data = data.Slice(0, copy_size);

                return data;
            }
            else 
                return BucketBytes.Empty;
        }

        public override bool CanReset => Inner.CanReset && BaseBucket.CanReset;

        public override string Name => "GitDelta[" + Inner.Name + "]>" + BaseBucket.Name;

        public async override ValueTask ResetAsync()
        {
            if (!CanReset)
                throw new InvalidOperationException($"Reset not supported on {Name} bucket");

            await Inner.ResetAsync(); // Default error text or source reset
            await BaseBucket.ResetAsync();

            state = delta_state.start;
            length = 0;
            position = 0;
            copy_offset = 0;
            copy_size = 0;
            p0 = 0;
        }
    }
}
