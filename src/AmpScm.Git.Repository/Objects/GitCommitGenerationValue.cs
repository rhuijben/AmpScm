using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Objects
{
    [CLSCompliant(false)]
    public struct GitCommitGenerationValue : IEquatable<GitCommitGenerationValue>
    {
        ulong value;

        public GitCommitGenerationValue(int generation, DateTimeOffset timeStamp)
        {
            if (generation < 0)
                throw new ArgumentOutOfRangeException(nameof(generation));
            else if (generation >= 0x3FFFFFFF)
                generation = 0x3FFFFFFF;

            var s = timeStamp.ToUnixTimeSeconds();

            if (s < 0)
                throw new ArgumentOutOfRangeException(nameof(timeStamp));

            if (s >= 0x3FFFFFFFF)
                s = 0x3FFFFFFFF; // Overflow. We should use overflow handling over 34 bit...
                                 // So somewhere before 2038 + 4 * (2038-1970)... 2242...

            value = ((ulong)generation << 34) | (ulong)s;
        }

        public GitCommitGenerationValue(int generation, long timeValue)
        {
            if (generation < 0)
                throw new ArgumentOutOfRangeException(nameof(generation));
            else if (generation >= 0x3FFFFFFF)
                generation = 0x3FFFFFFF;

            var s = timeValue;

            if (s < 0)
                throw new ArgumentOutOfRangeException(nameof(timeValue));

            if (s >= 0x3FFFFFFFF)
                s = 0x3FFFFFFFF; // Overflow. We should use overflow handling over 34 bit...
                                 // So somewhere before 2038 + 4 * (2038-1970)... 2242...

            value = ((ulong)generation << 2) | (((ulong)s & 0x300000000) >> 32) | (((ulong)s & 0xFFFFFFFF) << 32);
        }

        public long CorrectedTimeValue => (long)(value & 0x3FFFFFFFF);

        public DateTimeOffset CorrectedTime => DateTimeOffset.FromUnixTimeSeconds(CorrectedTimeValue);

        public int Generation => (int)(value >> 34);


        public static GitCommitGenerationValue FromValue(ulong value)
        {
            return new GitCommitGenerationValue { value = value };
        }

        public ulong Value => value;

        public bool HasValue => value != 0;

        public override bool Equals(object obj)
        {
            return (obj is GitCommitGenerationValue other) && Equals(other);
        }

        public bool Equals(GitCommitGenerationValue other)
        {
            return other.Value == Value;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public static bool operator ==(GitCommitGenerationValue left, GitCommitGenerationValue right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GitCommitGenerationValue left, GitCommitGenerationValue right)
        {
            return !(left == right);
        }
    }
}
