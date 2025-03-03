﻿using System;
using System.Threading.Tasks;

namespace AmpScm.Buckets
{
    public partial class Bucket
    {
        internal async ValueTask<byte?> ReadByteAsync()
        {
            var r = await ReadAsync(1).ConfigureAwait(false);

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
                var r = await ReadSkipAsync(int.MaxValue).ConfigureAwait(false);

                skip -= r;
                totalSkip += r;

                if (r == 0)
                    return totalSkip;
            }

            while (skip > 0)
            {
                var r = await ReadSkipAsync((int)skip).ConfigureAwait(false);

                skip -= r;
                totalSkip += r;

                if (r == 0)
                    return totalSkip;
            }

            return totalSkip;
        }
    }
}
