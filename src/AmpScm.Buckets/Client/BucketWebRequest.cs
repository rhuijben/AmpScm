using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Client;
using AmpScm.Buckets.Client.Protocols;

namespace AmpScm.Buckets.Client
{
    public abstract class BucketWebRequest : IAsyncDisposable, IDisposable
    {
        protected BucketWebClient Client {get; }
        private bool _disposed;

        public Uri RequestUri { get; }

        public virtual string? Method 
        { 
            get => null!;
            set => throw new InvalidOperationException();
        }

        public Client.WebHeaderDictionary Headers { get; } = new Client.WebHeaderDictionary();

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

        public bool FollowRedirects { get; set; } = true;

        protected BucketWebRequest(BucketWebClient client, Uri requestUri)
        {
            Client = client ?? throw new ArgumentNullException(nameof(client));
            RequestUri = requestUri ?? throw new ArgumentNullException(nameof(requestUri));
        }

        public abstract ValueTask<ResponseBucket> GetResponseAsync();

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                _disposed = true;
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

        public event EventHandler<BasicBucketAuthenticationEventArgs> BasicAuthentication;

        internal EventHandler<BasicBucketAuthenticationEventArgs> GetBasicAuthenticationHandlers()
        {
            return BasicAuthentication + Client.GetBasicAuthenticationHandlers();
        }
    }
}
