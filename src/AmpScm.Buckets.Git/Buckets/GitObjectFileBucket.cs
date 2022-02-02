using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Specialized;
using AmpScm.Git;

namespace AmpScm.Buckets.Git
{
    public sealed class GitObjectFileBucket : GitObjectBucket
    {
        long _startOffset;
        long? _length;

        public GitObjectFileBucket(Bucket inner) 
            : base(new ZLibBucket(inner))
        {
        }

        public override async ValueTask ReadTypeAsync()
        {
            if (_startOffset == 0)
            {
                var (bb, eol) = await Inner.ReadUntilEolFullAsync(BucketEol.Zero, null);

                if (Type == default)
                {
                    switch(bb[0])
                    {
                        case (byte)'b':
                            Type = GitObjectType.Blob;
                            break;
                        case (byte)'c':
                            Type = GitObjectType.Commit;
                            break;
                        case (byte)'r': // If second char
                        case (byte)'t' when bb.Length > 1 && bb[1] == (byte)'r':
                            Type = GitObjectType.Tree;
                            break;
                        case (byte)'a': // If second char
                        case (byte)'t' when bb.Length > 1 && bb[1] == (byte)'a':
                            Type = GitObjectType.Tag;
                            break;
                        default:
                            if (bb.Length >= 2)
                                throw new GitBucketException("Unexpected type");
                            break;
                    }
                }

                if (eol == BucketEol.Zero)
                {
                    int nSize = bb.IndexOf((byte)' ');

                    if (nSize > 0 && long.TryParse(bb.ToASCIIString(nSize + 1, bb.Length - nSize - 1, eol), out var len))
                        _length = len;

                    _startOffset = Inner.Position!.Value;
                }
            }
        }

        public override long? Position
        {
            get
            {
                if (_startOffset == 0)
                    return 0;

                var p = Inner.Position;

                if (p < _startOffset)
                    return 0;
                else
                    return p - _startOffset;
            }
        }

        public async override ValueTask<long?> ReadRemainingBytesAsync()
        {
            await ReadTypeAsync();

            if (_length.HasValue)
            {
                return _length - Position;
            }
            else
                return null;
        }

        public override ValueTask<BucketBytes> PeekAsync()
        {
            if (_startOffset == 0)
                return EmptyTask;
            else
                return Inner.PeekAsync();
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            if (_startOffset == 0)
                await ReadTypeAsync();
            
            return await Inner.ReadAsync(requested);
        }

        public override bool CanReset => Inner.CanReset;

        public async override ValueTask ResetAsync()
        {
            await base.ResetAsync();

            _startOffset = 0; // Handles skip and offset
            // Keep Type and Length values
        }
    }
}
