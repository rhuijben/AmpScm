using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Protocols;

namespace AmpScm.Buckets.Client
{
    public class BucketWebClient : IDisposable
    {
        private bool disposedValue;

        public BucketWebRequest CreateRequest(Uri requestUri)
        {
            if (requestUri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase))
                return new BucketHttpRequest(this, requestUri);
            else if (requestUri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
                return new BucketHttpsRequest(this, requestUri);
            else
                throw new NotSupportedException();
        }

        public BucketWebRequest CreateRequest(string url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return CreateRequest(uri);
            else
                throw new ArgumentOutOfRangeException(url);
        }

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
        // ~BucketWebClient()
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
    }
}
