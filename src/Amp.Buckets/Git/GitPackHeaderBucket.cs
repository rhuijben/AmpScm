using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Amp.Buckets.Git
{
    public class GitPackHeaderBucket : GitBucket
    {
        BucketStructCollector<GitPackHeader> header;

        public GitPackHeaderBucket(Bucket inner) : base(inner)
        {
        }

        public override ValueTask<BucketBytes> PeekAsync(bool noPoll = false)
        {
            return BucketBytes.Empty;
        }

        public async override ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            var h = await header.ReadAsync(Inner);

            return BucketBytes.Eof;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        struct GitPackHeader
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 4)]
            public string GitType;
            [NetworkOrder]
            public int Version;
            [NetworkOrder]
            public int ObjectCount;
        }
    }
}
