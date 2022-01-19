using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    }
}
