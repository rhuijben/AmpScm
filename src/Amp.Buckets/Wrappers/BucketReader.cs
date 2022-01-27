using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Amp.Buckets.Wrappers
{
    public class BucketReader : TextReader
    {
        readonly byte[] buffer = new byte[16];
        int _next;
        public BucketReader(Bucket bucket, Encoding? textEncoding)
        {
            Bucket = bucket ?? throw new ArgumentNullException(nameof(Bucket));
            TextEncoding = textEncoding;
            _next = -1;
        }

        public Bucket Bucket { get; }

        public Encoding? TextEncoding { get; }


        public override int Read()
        {
            if (_next >= 0)
            {
                _next = -1;
                return _next;
            }
            var v = Bucket.ReadAsync(1);

            BucketBytes b = v.IsCompleted ? v.Result : v.GetAwaiter().GetResult();

            if (b.IsEof || b.IsEmpty)
                return -1;

            return b[0];
        }

        public override int Peek()
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

            var v = Bucket.PeekAsync();

            BucketBytes b = v.IsCompleted ? v.Result : v.GetAwaiter().GetResult();

            if (!b.IsEmpty)
                return b[0];

            v = Bucket.ReadAsync(1);

            b = v.IsCompleted ? v.Result : v.GetAwaiter().GetResult();

            if (b.IsEof || b.IsEmpty)
                return -1;

            return _next = b[0];
        }

        public override int Read(char[] buffer, int index, int count)
        {
            var v = Bucket.PeekAsync();

            BucketBytes b = v.IsCompleted ? v.Result : v.GetAwaiter().GetResult();

            if (!b.IsEmpty)
            {
                // THIS variant should work, minus encoding issues
                // we can leave broken chars, etc.
                for (int i = 0; i < count && i < b.Length; i++)
                    buffer[index++] = (char)b[i]; // TODO: Apply encoding!

                v = Bucket.ReadAsync(b.Length);

                b = v.IsCompleted ? v.Result : v.GetAwaiter().GetResult();

                return b.Length;
            }
            else
            {
                // THIS is an ugly hack^2
                v = Bucket.ReadAsync(count);

                b = v.IsCompleted ? v.Result : v.GetAwaiter().GetResult();

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
