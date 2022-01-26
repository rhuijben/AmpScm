using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Amp.Buckets.Specialized;

namespace Amp.Buckets.Git
{
    public enum GitObjectType
    {
        None = 0, // Reserved. Unused

        // These types are valid objects
        Commit = 1,
        Tree = 2,
        Blob = 3,
        Tag = 4,

        // These types are in pack files, but not real objects
        DeltaOffset = 6,
        DeltaReference = 7
    };

    public sealed class GitPackFrameBucket : GitBucket
    {
        Bucket wrapped => Inner;
        Bucket? reader;
        frame_state state;
        long body_size;
        long position;
        long frame_position;
        long delta_position;
        GitObjectId _oid;

        enum frame_state
        {
            start,
            size_done,
            find_delta,
            body
        }

        public GitObjectType Type { get; private set; }
        public int? DeltaCount { get; private set; }
        public long? BodySize { get; private set; }

        public override string Name => (reader != null) ? $"GitPackFrame[{reader.Name}]>{Inner.Name}" : base.Name;


        public GitPackFrameBucket(Bucket inner, GitObjectIdType oidType)
            : base(inner.WithPosition())
        {
            _oid = new GitObjectId(oidType, Array.Empty<byte>());
        }

        public override ValueTask<BucketBytes> PeekAsync()
        {
            return EmptyTask;
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            if (reader == null || state != frame_state.body)
            {
                if (!await ReadInfoAsync())
                    return BucketBytes.Eof;
            }

            return await reader!.ReadAsync(requested);
        }

        public override async ValueTask<int> ReadSkipAsync(int requested)
        {
            if (reader == null || state != frame_state.body)
            {
                if (!await ReadInfoAsync())
                    return 0;
            }

            return await reader!.ReadSkipAsync(requested);
        }

        public async ValueTask<bool> ReadInfoAsync()
        {
            if (state < frame_state.body)
            {
                const long max_size_len = 1 + (64 - 4 + 6) / 7;

                while (state == frame_state.start)
                {
                    // In the initial state we use position to keep track of our
                    // location withing the compressed length

                    var peeked = await Inner.PeekAsync();

                    int rq_len;

                    if (!peeked.IsEmpty)
                    {
                        rq_len = 0;
                        for (int i = 0; i <= max_size_len && i < peeked.Length; i++)
                        {
                            rq_len++;
                            if (0 == (peeked[i] & 0x80))
                                break;
                        }
                        rq_len = Math.Min(rq_len, peeked.Length);
                    }
                    else
                        rq_len = 1;

                    var read = await Inner.ReadAsync(rq_len);

                    for (int i = 0; i < read.Length; i++)
                    {
                        byte uc = read[i];

                        if (position == 0)
                        {
                            Type = (GitObjectType)((uc >> 4) & 0x7);
                            body_size = uc & 0xF;

                            long my_offs = Inner.Position!.Value;
                            if (my_offs >= 0)
                                frame_position = my_offs - read.Length;
                        }
                        else
                            body_size |= (long)(uc & 0x7F) << (4 + 7 * ((int)position - 1));

                        if (0 == (uc & 0x80))
                        {
                            if (position > max_size_len)
                                throw new InvalidOperationException("Git pack framesize overflows int64");

                            if (Type == GitObjectType.None)
                                throw new InvalidOperationException("Git pack frame 0 is invalid");
                            else if ((int)Type == 5)
                                throw new InvalidOperationException("Git pack frame 5 is unsupported");

                            Debug.Assert(i == read.Length - 1);
                            state = frame_state.size_done;
                            position = 0;
                        }
                        else
                            position++;
                    }
                }

                while (state == frame_state.size_done)
                {
                    if (Type == GitObjectType.DeltaReference)
                    {
                        throw new NotImplementedException("No Delta Reference support yet");
                        //// Body starts with oid refering to the delta base
                        //ptrdiff_t base_len;
                        //
                        //if (git_type == amp_git_delta_ref || git_type == amp_git_delta_ofs)
                        //	base_len = (base_oid.type == amp_git_oid_sha1) ? 20 : 32;
                        //else
                        //	base_len = 0;
                        //
                        //AMP_ASSERT(position <= base_len);
                        //amp_span read;
                        //
                        //if (base_len > position)
                        //	AMP_ERR((*wrapped)->read(&read, base_len - (ptrdiff_t)position, scratch_pool));
                        //
                        //if (read.size_bytes())
                        //{
                        //	memcpy(base_oid.bytes + position, read.data(), read.size_bytes());
                        //	position += read.size_bytes();
                        //}
                        //
                        //if (position >= base_len)
                        //{
                        //	AMP_ASSERT(position == base_len);
                        //	position = 0; // And now start the real body
                        //	state = state::find_delta;
                        //	break;
                        //}
                    }
                    else if (Type == GitObjectType.DeltaOffset)
                    {
                        // Body starts with negative offset of the delta base.
                        long max_delta_size_len = 1 + (64 + 6) / 7;

                        var peeked = await Inner.PeekAsync();
                        int rq_len;

                        if (!peeked.IsEmpty)
                        {
                            rq_len = 0;
                            for (int i = 0; i <= max_delta_size_len && i < peeked.Length; i++)
                            {
                                rq_len++;
                                if (0 == (peeked[i] & 0x80))
                                    break;
                            }
                            rq_len = Math.Min(rq_len, peeked.Length);
                        }
                        else
                            rq_len = 1;

                        var read = await Inner.ReadAsync(rq_len);

                        for (int i = 0; i < read.Length; i++)
                        {
                            byte uc = read[i];

                            if (position > 0)
                                delta_position = (delta_position + 1) << 7;

                            delta_position |= (long)(uc & 0x7F);
                            position++;

                            if (0 == (uc & 0x80))
                            {
                                if (position > max_delta_size_len)
                                    throw new InvalidOperationException("Git pack delta reference overflows 64 bit integer");
                                else if (delta_position > frame_position)
                                    throw new InvalidOperationException("Delta position must point to earlier object in file");

                                Debug.Assert(i == read.Length - 1);
                                state = frame_state.find_delta;
                                position = 0;
                                delta_position = frame_position - delta_position;
                                reader = new ZLibBucket(Inner.SeekOnReset().NoClose());
                                BodySize = body_size;
                            }
                        }
                    }
                    else
                    {
                        position = 0; // The real body starts right now
                        state = frame_state.body;
                        reader = new ZLibBucket(Inner.SeekOnReset().NoClose());
                        DeltaCount = 0;
                        BodySize = body_size;
                    }
                }

                while (state == frame_state.find_delta)
                {
                    Bucket? base_reader = null;

                    if (Type == GitObjectType.DeltaOffset)
                    {
                        // TODO: This is not restartable via async handling, while it should be.

                        // The source needs support for this. Our filestream and memorystreams have this support

                        Bucket deltaSource = await Inner.DuplicateAsync(true);
                        long to_skip = delta_position;

                        while (to_skip > 0)
                        {
                            var skipped = await deltaSource.ReadSkipAsync(to_skip);

                            if (skipped == 0)
                                return false; // EOF

                            to_skip -= skipped;
                        }

                        base_reader = new GitPackFrameBucket(deltaSource, _oid.Type);
                    }
                    else
                        throw new NotImplementedException("Can't obtain delta reference (via oid not implemented yet)");

                    if (base_reader is GitPackFrameBucket fb)
                    {
                        GitObjectType base_type;

                        if (!await fb.ReadInfoAsync())
                            return false;

                        base_type = fb.Type;
                        DeltaCount = fb.DeltaCount + 1;

                        state = frame_state.body;
                        Type = base_type; // type is now resolved
                    }

                    reader = new GitDeltaBucket(reader!, base_reader);
                }
            }

            return true;
        }

        public override long? Position => (state == frame_state.body) ? reader!.Position : 0;

        public override async ValueTask<long?> ReadRemainingBytesAsync()
        {
            if (state < frame_state.body)
            {
                var b = await ReadInfoAsync();

                if (!b)
                    return null;
            }

            if (DeltaCount > 0)
                return await reader!.ReadRemainingBytesAsync();
            else
                return body_size - reader!.Position;
        }

        public async override ValueTask ResetAsync()
        {
            if (state < frame_state.body)
                return; // Nothing to reset

            await reader!.ResetAsync();
        }

        public override bool CanReset => Inner.CanReset;
    }
}
