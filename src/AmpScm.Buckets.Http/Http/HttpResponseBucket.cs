using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Buckets.Http
{
    public class HttpResponseBucket : ResponseBucket
    {
        bool _readHeaders;
        BucketEolState? _state;
        Bucket ? _reader;
        private bool _doneAtEof;

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
                bool chunked = false;
                bool allowNext = false;

                // Transfer-Encoding, aka Hop by hop encoding. Typically 'chunked'
                if (ResponseHeaders[HttpResponseHeader.TransferEncoding] is string te)
                {
                    foreach(var tEnc in te.Split(new[] { ',' }))
                    {
                        if (string.Equals(tEnc, "chunked", StringComparison.OrdinalIgnoreCase))
                        {
                            allowNext = chunked = true;
                            rdr = new HttpDechunkBucket(rdr, true);
                        }
                    }
                }

                // RFC 7231 specifies that we should determine the message length via Transfer-Encoding
                // chunked, when both chunked and Content-Length are passed
                if (!chunked && ResponseHeaders[HttpResponseHeader.ContentLength] is string cl
                    && long.TryParse(cl, out var contentLength) && contentLength >= 0)
                {
                    rdr = rdr.Take(contentLength, true).NoClose();
                    allowNext = true;
                }

                // Content-Encoding, aka end-to-end encoding. Typically 'gzip'
                if (ResponseHeaders[HttpResponseHeader.ContentEncoding] is string ce)
                {
                    foreach (var cEnc in ce.Split(new[] { ',' }))
                    {
                        if (string.Equals(cEnc, "gzip", StringComparison.OrdinalIgnoreCase))
                        {
                            rdr = rdr.Decompress(BucketCompressionAlgorithm.GZip);
                        }
                        else if (string.Equals(cEnc, "deflate", StringComparison.OrdinalIgnoreCase))
                        {
                            rdr = rdr.Decompress(BucketCompressionAlgorithm.Deflate);
                        }
#if !NETFRAMEWORK
                        else if (string.Equals(cEnc, "br", StringComparison.OrdinalIgnoreCase))
                        {
                            rdr = rdr.Decompress(BucketCompressionAlgorithm.Brotli);
                        }
#endif
                    }
                }

                _doneAtEof = !allowNext;
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

            if (!HttpStatus.HasValue)
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

        public async ValueTask<int> ReadStatus()
        {
            if (HttpStatus.HasValue)
                return HttpStatus.Value!;

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
            return status;
        }
    }
}
