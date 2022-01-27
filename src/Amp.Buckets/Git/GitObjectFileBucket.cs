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
                int toRead;
                var bb = await Inner.PeekAsync();

                if (!bb.IsEmpty)
                    toRead = 0;
                else
                {
                    bb = await Inner.ReadAsync(1);

                    toRead = -bb.Length;

                    if (!bb.IsEmpty)
                    {
                        // We read one byte, and that might be the first byte of a new huge peek buffer
                        // Let's check if this first byte is just that...

                        byte bOne = bb[0];
                        var peek = await Inner.PeekAsync();

                        if (peek.IsEmpty)
                        {
                            // Too bad, we are probably at eof.
                            bb = new byte[] { bOne };
                        }
                        else
                        {
                            var (tb, offs, len) = peek.ExpandToArray(false);

                            if (tb is not null && offs > 0 && tb[offs - 1] == bOne)
                            {
                                // Nice guess. The peek buffer contains the read byte
                                bb = new BucketBytes(tb, offs - 1, len + 1);
                            }
                            else if (tb is not null)
                            {
                                // Bad case, the read byte is not in the buffer.
                                // Let's create something else

                                byte[] buf = new byte[Math.Min(64, 1 + peek.Length)];
                                buf[0] = bOne;
                                for (int i = 1; i < buf.Length; i++)
                                    buf[i] = peek[i - 1];

                                bb = buf;
                            }
                            else
                            {
                                // Auch, we got a span backed by something else than an array
                                bb = new byte[] { bOne };
                            }
                        }
                    }
                }

                if (!bb.IsEmpty)
                {
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
                            startOffset = Inner.Position!.Value + toRead + i + 1;
                            toRead += i + 1;
                            readHeader = true;                            

                            break;
                        }
                    }

                    if (!readHeader)
                        toRead += bb.Length;

                    if (toRead > 0)
                    {
                        bb = await Inner.ReadAsync(toRead);

                        if (bb.Length != toRead)
                            throw new GitBucketException("Peeked data not readable");
                    }
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
