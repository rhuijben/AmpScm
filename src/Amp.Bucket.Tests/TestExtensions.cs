using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amp.Buckets;

namespace Amp.BucketTests
{
    internal static class TestExtensions
    {
        public static void ReadFull(this Bucket self,  byte[] array)
        {
            int pos = 0;

            while (pos < array.Length)
            {
                var r = self.ReadAsync(array.Length - pos);

                BucketBytes bb;

                if (r.IsCompleted)
                    bb = r.Result;
                else
                    bb = r.GetAwaiter().GetResult();

                bb.CopyTo(new Memory<byte>(array, pos, bb.Length));
                pos += bb.Length;
                if (bb.IsEof)
                    throw new InvalidOperationException();
            }
        }
    }
}
