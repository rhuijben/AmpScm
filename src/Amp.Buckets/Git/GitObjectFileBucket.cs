using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amp.Buckets.Specialized;

namespace Amp.Buckets.Git
{
    public class GitObjectFileBucket : GitBucket, IGitObjectType
    {
        bool readHeader;
        long startOffset;
        long length;
        public GitObjectFileBucket(Bucket inner) 
            : base(new ZLibBucket(inner))
        {
        }

        public GitObjectType Type { get; private set; }

        public async override ValueTask<long?> ReadRemainingBytesAsync()
        {
            await ReadInfo();
            return await base.ReadRemainingBytesAsync();
        }

        private async ValueTask ReadInfo()
        {
            if (!readHeader)
            {
                using var poll = await Inner.PollAsync(7); // "blob 0\0"

                if (!poll.Data.IsEmpty)
                {
                    var bb = poll.Data;

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
                            case (byte)'t' when bb.Length > 1 && bb[1] == (byte)'r':
                                Type = GitObjectType.Tree;
                                break;
                            case (byte)'t' when bb.Length > 1 && bb[1] == (byte)'a':
                                Type = GitObjectType.Tag;
                                break;
                            default:
                                if (bb.Length >= 2)
                                    throw new GitBucketException("Unexpected type");
                                break;
                        }
                    }

                    for(int i = 0; i < bb.Length; i++)
                    {
                        if (bb[i] == 0)
                        {
                            startOffset = poll.Position!.Value + i + 1;
                            readHeader = true;                            

                            break;
                        }
                    }

                    if (startOffset > 0)
                        await poll.Consume((int)startOffset);
                    else
                        await poll.Consume(poll.Length);
                }
            }
        }

        public override ValueTask<BucketBytes> PeekAsync()
        {
            if (!readHeader)
                return EmptyTask;
            else
                return Inner.PeekAsync();
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            if (!readHeader)
                await ReadInfo();
            
            return await Inner.ReadAsync(requested);
        }
    }
}
