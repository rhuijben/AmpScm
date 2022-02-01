using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Amp.Buckets.Specialized;
using Amp.Git;

namespace Amp.Buckets.Git
{
    public sealed class GitPackFrameBucket : GitBucket, IGitObjectType
    {
        Bucket wrapped => Inner;
        Bucket? reader;
        frame_state state;
        long body_size;
        long position;
        long frame_position;
        long delta_position;
        GitObjectIdType _oidType;
        Func<GitObjectId, ValueTask<GitBucket>>? _oidResolver;
        byte[]? _deltaOid;

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

        // These types are in pack files, but not real objects
        const GitObjectType GitObjectType_DeltaOffset = (GitObjectType)6;
        const GitObjectType GitObjectType_DeltaReference = (GitObjectType)7;

        public GitPackFrameBucket(Bucket inner, GitObjectIdType oidType, Func<GitObjectId, ValueTask<GitBucket>>? resolveOid = null)
            : base(inner.WithPosition())
        {
            _oidType = oidType;
            _oidResolver = resolveOid;
        }

        public override ValueTask<BucketBytes> PeekAsync()
        {
            if (reader == null || state != frame_state.body)
                return EmptyTask;

            return reader.PeekAsync();
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

        public async ValueTask ReadTypeAsync()
        {
            await ReadInfoAsync();
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
                                throw new GitBucketException("Git pack framesize overflows int64");

                            if (Type == GitObjectType.None)
                                throw new GitBucketException("Git pack frame 0 is invalid");
                            else if ((int)Type == 5)
                                throw new GitBucketException("Git pack frame 5 is unsupported");

                            Debug.Assert(i == read.Length - 1);
                            state = frame_state.size_done;
                            position = 0;
                            BodySize = body_size;
                        }
                        else
                            position++;
                    }
                }

                while (state == frame_state.size_done)
                {
                    if (Type == GitObjectType_DeltaReference)
                    {
                        if (_deltaOid == null)
                        {
                            _deltaOid = new byte[(_oidType == GitObjectIdType.Sha1) ? 20 : 32];
                            position = 0;
                        }

                        while (position < _deltaOid.Length)
                        {
                            var read = await Inner.ReadAsync(_deltaOid.Length - (int)position);

                            if (read.IsEof)
                                return false;

                            for (int i = 0; i < read.Length; i++)
                                _deltaOid[position++] = read[i];
                        }

                        state = frame_state.find_delta;
                        position = 0;
                        reader = new ZLibBucket(Inner.SeekOnReset().NoClose());
                    }
                    else if (Type == GitObjectType_DeltaOffset)
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
                                    throw new GitBucketException("Git pack delta reference overflows 64 bit integer");
                                else if (delta_position > frame_position)
                                    throw new GitBucketException("Delta position must point to earlier object in file");

                                Debug.Assert(i == read.Length - 1);
                                state = frame_state.find_delta;
                                position = 0;
                                delta_position = frame_position - delta_position;
                                reader = new ZLibBucket(Inner.SeekOnReset().NoClose());
                            }
                        }
                    }
                    else
                    {
                        position = 0; // The real body starts right now
                        state = frame_state.body;
                        reader = new ZLibBucket(Inner.SeekOnReset().NoClose());
                        DeltaCount = 0;
                        _oidResolver = null;
                    }
                }

                while (state == frame_state.find_delta)
                {
                    Bucket? base_reader = null;

                    if (Type == GitObjectType_DeltaOffset)
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

                        base_reader = new GitPackFrameBucket(deltaSource, _oidType, _oidResolver);
                    }
                    else 
                    {
                        var deltaOid = new GitObjectId(_oidType, _deltaOid!);

                        if (_oidResolver != null)
                        {
                            base_reader = await _oidResolver(deltaOid);
                        }

                        if (base_reader == null)
                            throw new GitBucketException($"Can't obtain delta reference for {deltaOid}");
                        _deltaOid = null; // Not used any more
                    }

                    if (base_reader is GitPackFrameBucket fb)
                    {
                        if (!await fb.ReadInfoAsync())
                            return false;

                        DeltaCount = fb.DeltaCount + 1;
                        state = frame_state.body;
                        Type = fb.Type; // type is now resolved
                    }
                    else if (base_reader is IGitObjectType bt)
                    {
                        state = frame_state.body;
                        Type = bt.Type; // type is now resolved
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
