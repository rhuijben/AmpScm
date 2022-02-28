using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Client.Protocols;
using AmpScm.Buckets.Specialized;
using BasicHandler = System.EventHandler<AmpScm.Buckets.Client.BasicBucketAuthenticationEventArgs>;

namespace AmpScm.Buckets.Client.Http
{
    public class HttpResponseBucket : ResponseBucket
    {
        BucketEolState? _state;
        Bucket? _reader;
        private bool _doneAtEof;
        WebHeaderDictionary? _responseHeaders;
        int _nRedirects;
        Action? _succes;
        Action? _authFailed;

        public string? HttpVersion { get; private set; }
        public int? HttpStatus { get; private set; }
        public string? HttpMessage { get; private set; }

        internal HttpResponseBucket(Bucket inner, BucketHttpRequest request)
            : base(inner, request)
        {

        }

        public new BucketHttpRequest Request => (BucketHttpRequest)base.Request;

        public override async ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            if (_responseHeaders is null)
                await ReadHeaders().ConfigureAwait(false);

            if (_reader == null)
            {
                var (reader, doneAtEof) = GetBodyReader(Headers);

                _doneAtEof = doneAtEof;
                _reader = reader;
            }

            var bb = await _reader.ReadAsync(requested).ConfigureAwait(false);

            if (bb.IsEof)
            {
                if (Request.Channel != null)
                {
                    await _reader.DisposeAsync().ConfigureAwait(false);
                    _reader = Bucket.Empty;

                    Request.ReleaseChannel();
                }
            }
            return bb;
        }

        private (Bucket reader, bool doneAtEof) GetBodyReader(WebHeaderDictionary headers)
        {
            var rdr = Inner;
            bool chunked = false;
            bool allowNext = false;

            // Transfer-Encoding, aka Hop by hop encoding. Typically 'chunked'
            if (headers[HttpResponseHeader.TransferEncoding] is string te)
            {
                foreach (var tEnc in te.Split(new[] { ',' }))
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
            if (!chunked && headers[HttpResponseHeader.ContentLength] is string cl
                && long.TryParse(cl, out var contentLength) && contentLength >= 0)
            {
                rdr = rdr.Take(contentLength, true).NoClose();
                allowNext = true;
            }

            // Content-Encoding, aka end-to-end encoding. Typically 'gzip'
            if (headers[HttpResponseHeader.ContentEncoding] is string ce)
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
                    /*
                    else if (string.Equals(cEnc, "zstd", StringComparison.OrdinalIgnoreCase))
                    {
                        rdr = rdr.Decompress(BucketCompressionAlgorithm.Zstd); // Easy to implement via https://www.nuget.org/packages/ZstdSharp.Port
                    }
                    */
                }
            }

            return (rdr, !allowNext);
        }

        public override BucketBytes Peek()
        {
            if (_reader is not null)
                return _reader.Peek();

            return base.Peek();
        }

        public override async ValueTask ReadHeaders()
        {
            if (_responseHeaders is not null)
                return;

            if (!HttpStatus.HasValue)
                await ReadStatus().ConfigureAwait(false);

            _responseHeaders ??= await ReadHeaderSet().ConfigureAwait(false);
        }

        private async ValueTask<WebHeaderDictionary> ReadHeaderSet()
        {
            WebHeaderDictionary whc = new WebHeaderDictionary();
            var (bb, eol) = await Inner.ReadUntilEolFullAsync(BucketEol.AnyEol, _state).ConfigureAwait(false);
            while (bb.Length - eol.CharCount() > 0)
            {
                string line = bb.ToUTF8String(eol);

                string[] parts = line.Split(new[] { ':' }, 2);

                whc[parts[0]] = parts[1].Trim();

                (bb, eol) = await Inner.ReadUntilEolFullAsync(BucketEol.AnyEol, _state).ConfigureAwait(false);
            }

            return whc;
        }

        public async ValueTask<int> ReadStatus()
        {
            if (HttpStatus.HasValue)
                return HttpStatus.Value!;

            while (true)
            {
                _state = new BucketEolState();
                var (bb, eol) = await Inner.ReadUntilEolFullAsync(BucketEol.AnyEol, _state).ConfigureAwait(false);

                string line = bb.ToASCIIString(eol);

                var parts = line.Split(new[] { ' ' }, 3);

                if (parts[0].StartsWith("HTTP/", StringComparison.OrdinalIgnoreCase) && parts.Length == 3)
                    HttpVersion = parts[0].Substring(1);
                else
                    throw new HttpBucketException($"No HTTP result: {line}");

                if (int.TryParse(parts[1], out var status) && status >= 100 && status < 1000)
                {
                    switch (status)
                    {
                        case (int)HttpStatusCode.TemporaryRedirect:
#if !NETFRAMEWORK
                        case (int)HttpStatusCode.PermanentRedirect:
#else
                        case 308:
#endif
                            if (Request.FollowRedirects && _nRedirects < 10)
                            {
                                _nRedirects++;
                                await HandleRedirect().ConfigureAwait(false);
                                continue;
                            }
                            break;
                        case (int)HttpStatusCode.Unauthorized:
                            _authFailed?.Invoke();
                            _authFailed = null;
                            if (await HandleAuthorization().ConfigureAwait(false))
                                continue;
                            break;
                    }

                    HttpStatus = status;

                    if (_succes != null && status >= 200 && status < 400)
                    {
                        _succes.Invoke();
                        _succes = null;
                    }
                }
                else
                    throw new HttpBucketException($"No Proper HTTP status: {line}");

                HttpMessage = parts[2];
                return status;
            }
        }



        IEnumerable<(string username, string password, string q, Action success, Action failed)> WalkAuthorization(Uri uri, string realm)
        {
            var handlers = Request.GetBasicAuthenticationHandlers();
            List<BasicHandler> hlrs = new List<BasicHandler>();

            if (handlers is MulticastDelegate md)
            {
                hlrs.AddRange(md.GetInvocationList().Cast<BasicHandler>());

                for (int i = 0; i < hlrs.Count; i++)
                {
                    if (hlrs[i] is MulticastDelegate md2)
                    {
                        var p = md.GetInvocationList();

                        if (p.Length != 1 || p[0] != md2)
                        {
                            hlrs.RemoveAt(i);

                            hlrs.InsertRange(i, p.Cast<BasicHandler>());
                            i--;
                            continue;
                        }
                    }
                }
            }
            else
                hlrs.Add(handlers);

            foreach (var h in hlrs)
            {
                BasicBucketAuthenticationEventArgs? e;
                do
                {
                    e = new BasicBucketAuthenticationEventArgs(uri, realm);
                    h.Invoke(this, e);

                    if (e.Handled)
                    {
                        yield return (e.Username ?? "", e.Password ?? "", "Basic", e.OnSucceeded, e.OnFailed);
                    }
                }
                while (e.Continue);
            }
        }

        IEnumerator<(string username, string password, string q, Action success, Action failed)>? _authState;

        private async ValueTask<bool> HandleAuthorization()
        {
            // Status line is read.. Read headers to allow authenticating in a new request
            var headers = await ReadHeaderSet().ConfigureAwait(false);

            string realm;

            if (headers[HttpResponseHeader.WwwAuthenticate] is string wwwAuthenticate)
            {
                var tk = wwwAuthenticate.Split(new[] { ' ' }, 2);
                string type = tk[0];

                switch (type.ToUpperInvariant())
                {
                    case "BASIC":
                        {
                            int n = tk[1].IndexOf("realm=\"", StringComparison.OrdinalIgnoreCase);

                            if (n >= 0)
                                realm = tk[1].Substring(n + 6, tk[1].IndexOf('\"', 7) - 6);
                            else
                                realm = "";
                        }
                        break;
                    default:
                        _responseHeaders = headers;
                        return false; // Just handle request
                }
            }
            else
            {
                _responseHeaders = headers;
                return false; //
            }

            _authState ??= WalkAuthorization(Request.RequestUri, realm).GetEnumerator();

            if (!_authState.MoveNext())
            {
                _responseHeaders = headers;
                return false; // Just handle request
            }

            var (reader, noClose) = GetBodyReader(headers);

            await reader.ReadSkipUntilEofAsync().ConfigureAwait(false);
            await reader.DisposeAsync().ConfigureAwait(false);

            var c = _authState.Current;

            Request.Headers[HttpRequestHeader.Authorization] = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{c.username}:{c.password}"));

            Request.Channel!.Writer.Write(Request.CreateRequest()); // Request same page again
            return true; // Read next result
        }

        internal void HandlePreAuthenticate(BucketHttpRequest bucketHttpRequest)
        {
            if (_authState != null)
                throw new InvalidOperationException();

            _authState = WalkAuthorization(Request.RequestUri, "\"pre-authenticate\"").GetEnumerator();

            if (!_authState.MoveNext())
                return; // Nothing to do

            var c = _authState.Current;

            Request.Headers[HttpRequestHeader.Authorization] = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{c.username}:{c.password}"));
            _succes += c.success;
            _authFailed += c.failed;
        }

        private ValueTask<bool> HandleRedirect()
        {
            throw new NotImplementedException();
        }

        public override bool SupportsHeaders => true;
        public override WebHeaderDictionary Headers => _responseHeaders!;

        public override long ContentLength
        {
            get
            {
                var v = Headers;
                if (v is not null && v[HttpResponseHeader.ContentLength] is string cl
                    && long.TryParse(cl, out var contentLength) && contentLength >= 0)
                {
                    return contentLength;
                }

                return -1;
            }
        }
    }
}
