using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Buckets.Http
{
    class HttpResponseBucket : ResponseBucket
    {
        bool _readStatus;
        bool _readHeaders;
        BucketEolState? _state;
        Bucket ? _reader;

        public string? HttpVersion { get; private set; }
        public int? HttpStatus { get; private set; }
        public string? HttpMessage { get;private set; }

        public WebHeaderCollection ResponseHeaders { get; private set; } = null!;

        public HttpResponseBucket(Bucket inner)
            : base(inner)
        {

        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            if (!_readHeaders)
                await ReadHeaders();
            
            if (_reader == null)
            {
                var rdr = Inner;

                if (ResponseHeaders[HttpResponseHeader.TransferEncoding] is string te)
                {
                    GC.KeepAlive(te);
                }

                if (ResponseHeaders[HttpResponseHeader.ContentLength] is string cl
                    && long.TryParse(cl, out var contentLength) && contentLength >= 0)
                {
                    rdr = rdr.Take(contentLength);
                }

                _reader = rdr;
            }

            return await _reader.ReadAsync(requested);
        }

        public override BucketBytes Peek()
        {
            if (_readHeaders && _reader is not null)
                return _reader.Peek();

            return base.Peek();
        }

        public override async ValueTask ReadHeaders()
        {
            if (_readHeaders)
                return;

            if (!_readStatus)
                await ReadStatus();

            ResponseHeaders ??= new WebHeaderCollection();
            var (bb, eol) = await Inner.ReadUntilEolFullAsync(BucketEol.AnyEol, _state);
            while(bb.Length - eol.CharCount() > 0)
            {
                string line = bb.ToUTF8String(eol);

                string[] parts = line.Split(new [] { ':' }, 2);

                ResponseHeaders[parts[0]] = parts[1].Trim();

                (bb, eol) = await Inner.ReadUntilEolFullAsync(BucketEol.AnyEol, _state);
            }

            _readHeaders = true;
        }

        public virtual async ValueTask ReadStatus()
        {
            if (_readStatus)
                return;

            _state = new BucketEolState();
            var (bb, eol) = await Inner.ReadUntilEolFullAsync(BucketEol.AnyEol, _state);

            string line = bb.ToASCIIString(eol);

            var parts = line.Split(new [] { ' ' }, 3);

            if (parts[0].StartsWith("HTTP/", StringComparison.OrdinalIgnoreCase) && parts.Length == 3)
                HttpVersion = parts[0].Substring(1);
            else
                throw new HttpBucketException($"No HTTP result: {line}");

            if (int.TryParse(parts[1], out var status) && status >= 100 && status < 1000)
                HttpStatus = status;
            else
                throw new HttpBucketException($"No Proper HTTP status: {line}");

            HttpMessage = parts[2];
            _readStatus = true;
        }
    }
}
