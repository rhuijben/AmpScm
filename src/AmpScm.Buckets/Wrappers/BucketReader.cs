using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Wrappers
{
    public class BucketReader : TextReader
    {
        int _next;
        public BucketReader(Bucket bucket, Encoding? textEncoding)
        {
            Bucket = bucket ?? throw new ArgumentNullException(nameof(bucket));
            TextEncoding = textEncoding;
            _next = -1;
        }

        public Bucket Bucket { get; }

        public Encoding? TextEncoding { get; }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                    Bucket.Dispose();
            }
            finally
            {
                base.Dispose(disposing);
            }            
        }


        public override int Read()
        {
            if (_next >= 0)
            {
                _next = -1;
                return _next;
            }
#pragma warning disable CA2012 // Use ValueTasks correctly
            var b = Bucket.ReadAsync(1).Result; // BAD async
#pragma warning restore CA2012 // Use ValueTasks correctly

            if (b.IsEof || b.IsEmpty)
                return -1;

            return b[0];
        }

        public override int Peek()
        {
#pragma warning disable CA2012 // Use ValueTasks correctly
            return PeekAsync().Result;
#pragma warning restore CA2012 // Use ValueTasks correctly
        }

        internal async ValueTask<int> PeekAsync()
        {
            if (_next >= 0)
            {
                try
                {
                    return _next;
                }
                finally
                {
                    _next = -1;
                }
            }

            BucketBytes b = Bucket.Peek();

            if (!b.IsEmpty)
                return b[0];

            b = await Bucket.ReadAsync(1).ConfigureAwait(false);

            if (b.IsEof || b.IsEmpty)
                return -1;

            return _next = b[0];
        }

        public override int Read(char[] buffer, int index, int count)
        {
#pragma warning disable CA2012 // Use ValueTasks correctly
            return ReadAsync(buffer, index, count).Result;
#pragma warning restore CA2012 // Use ValueTasks correctly
        }

        public override async Task<int> ReadAsync(char[] buffer, int index, int count)
        {
            if (buffer is null)
                throw new ArgumentNullException(nameof(buffer));

            var b = Bucket.Peek();

            if (!b.IsEmpty)
            {
                // THIS variant should work, minus encoding issues
                // we can leave broken chars, etc.
                for (int i = 0; i < count && i < b.Length; i++)
                    buffer[index++] = (char)b[i]; // TODO: Apply encoding!

                b = await Bucket.ReadAsync(b.Length).ConfigureAwait(false);

                return b.Length;
            }
            else
            {
                // THIS is an ugly hack^2
                b = await Bucket.ReadAsync(count).ConfigureAwait(false);

                if (b.IsEof)
                    return 0;

                for (int i = 0; i < count && i < b.Length; i++)
                    buffer[index++] = (char)b[i]; // TODO: Apply encoding!

                return b.Length;
            }
        }

        public override string? ReadLine()
        {
            //throw new NotImplementedException();
            return base.ReadLine();
        }

        public override string ReadToEnd()
        {
            //throw new NotImplementedException();
            return base.ReadToEnd();
        }
    }
}
