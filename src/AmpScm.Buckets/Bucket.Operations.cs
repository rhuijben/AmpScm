using System;
using System.Threading.Tasks;

namespace Amp.Buckets
{
    public partial class Bucket
    {
        internal async ValueTask<byte?> ReadByteAsync()
        {
            var r = await ReadAsync(1);

            if (r.Length != 1)
                return null;
            else
                return r.ToArray()[0];
        }

        public async ValueTask<long> ReadSkipAsync(long skip)
        {
            if (skip <= 0)
                throw new ArgumentOutOfRangeException(nameof(skip));

            long totalSkip = 0;

            while (skip > int.MaxValue)
            {
                var r = await ReadSkipAsync(int.MaxValue);

                skip -= r;
                totalSkip += r;

                if (r == 0)
                    return totalSkip;
            }

            while (skip > 0)
            {
                var r = await ReadSkipAsync((int)skip);

                skip -= r;
                totalSkip += r;

                if (r == 0)
                    return totalSkip;
            }

            return totalSkip;
        }
    }
}
