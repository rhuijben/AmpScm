using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Objects
{
    public struct GitCommitChainValue
    {
        ulong value;

        public GitCommitChainValue(int generation, DateTimeOffset stamp)
        {
            if (generation < 0)
                throw new ArgumentOutOfRangeException(nameof(generation));
            else if (generation >= 0x3FFFFFFF)
                generation = 0x3FFFFFFF;

            var s = stamp.ToUnixTimeSeconds();

            if (s < 0)
                throw new ArgumentOutOfRangeException(nameof(stamp));

            if (s >= 0x3FFFFFFFF)
                s = 0x3FFFFFFFF; // Overflow. We should use overflow handling over 34 bit...
                                 // So somewhere before 2038 + 4 * (2038-1970)... 2242...

            value = ((ulong)generation << 2) | (((ulong)s & 0x300000000) >> 32) | (((ulong)s & 0xFFFFFFFF) << 32);
        }

        public DateTimeOffset CorrectedTime => DateTimeOffset.FromUnixTimeSeconds((long)(value >> 32) | ((long)(value & 0x3)) << 32);

        public int Generation => (int)((value >> 2) & 0x3FFFFFFF);

        public static GitCommitChainValue FromValue(ulong value)
        {
            return new GitCommitChainValue { value = value };
        }

        public ulong Value => value;
    }

    public abstract class GitCommitChainItem
    {
        internal GitCommitChainItem()
        {


        }

        public IEnumerable<GitId> Parents { get; }



        internal sealed class GitCommitChainItemCommit : GitCommitChainItem
        {

        }

        internal sealed class GitCommitChainChainItem : GitCommitChainItem
        {
            public GitCommitChainChainItem(GitId[] parents)
            {

            }
        }
    }
}
