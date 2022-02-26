using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Protocols;

namespace AmpScm.Buckets
{
    public abstract class BucketWebRequest : IAsyncDisposable, IDisposable
    {
        private bool disposedValue;

        public Uri RequestUri { get; }

        public virtual string? Method 
        { 
            get => null!;
            set => throw new InvalidOperationException();
        }

        public Http.WebHeaderCollection Headers { get; } = new Http.WebHeaderCollection();

        public string? ContentType
        {
            get => Headers[HttpRequestHeader.ContentType];
            set => Headers[HttpRequestHeader.ContentType] = value;
        }

        public long? ContentLength
        {
            get => long.TryParse(Headers[HttpRequestHeader.ContentLength], out var v) && v >= 0 ? v : null;
            set => Headers[HttpRequestHeader.ContentLength] = value?.ToString(CultureInfo.InvariantCulture);
        }

        public bool PreAuthenticate { get; set; }

        protected BucketWebRequest(Uri requestUri)
        {
            RequestUri = requestUri ?? throw new ArgumentNullException(nameof(requestUri));
        }

        public static BucketWebRequest Create(Uri requestUri)
        {
            if (requestUri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase))
                return new BucketHttpRequest(requestUri);
            else if (requestUri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
                return new BucketHttpsRequest(requestUri);
            else
                throw new NotSupportedException();
        }

        public static BucketWebRequest Create(string url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return Create(uri);
            else
                throw new ArgumentOutOfRangeException(url);
        }

        public abstract ValueTask<Bucket> GetResponseAsync();

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~BucketWebRequest()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore().ConfigureAwait(false);
            Dispose(false);
            GC.SuppressFinalize(this);
        }

        protected virtual ValueTask DisposeAsyncCore()
        {
            return default;
        }

        public virtual string? Stats() => null;
    }
}
