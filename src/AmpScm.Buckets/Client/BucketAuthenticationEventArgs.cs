using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Client
{
    public class BucketAuthenticationEventArgs : EventArgs
    {
        internal BucketAuthenticationEventArgs(Uri uri, string realm)
        {
            Uri = uri;
            Realm = realm;
        }

        public string Realm { get; }
        public Uri Uri { get; }

        public bool Handled { get; set; } = true;
        public bool Continue { get; set; }

        public event EventHandler<BucketAuthenticationEventArgs>? Succeeded;
        public event EventHandler<BucketAuthenticationEventArgs>? Failed;

        internal void OnSucceeded() => Succeeded?.Invoke(this, this);

        internal void OnFailed() => Failed?.Invoke(this, this);
    }

    public class BasicBucketAuthenticationEventArgs : BucketAuthenticationEventArgs
    {
        internal BasicBucketAuthenticationEventArgs(Uri uri, string realm)
            : base(uri, realm)
        {
        }

        public string? Username { get; set; }
        public string? Password { get; set; }
    }
}
