using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Amp.Buckets.Specialized
{
    public sealed class CreateHashBucket: ProxyBucket
    {
        HashAlgorithm _hasher;
        byte[]? _result;
        Action<byte[]>? _onResult;

        public CreateHashBucket(Bucket inner, HashAlgorithm hasher)
            : base(inner)
        {
            _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
        }

        public CreateHashBucket(Bucket inner, HashAlgorithm hasher, Action<byte[]>? hashCreated)
            : this(inner, hasher)
        {
            _onResult = hashCreated;
        }

        public async override ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            var r = await Inner.ReadAsync(requested);

            if (r.IsEof)
            {
                _hasher.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                _result = _hasher.Hash;
                if (_result != null)
                    _onResult?.Invoke(_result);
            }
            else if (!r.IsEmpty)
                _hasher.TransformBlock(r.ToArray(), 0, r.Length, null!, 16);

            return r;
        }

        public override ValueTask<int> ReadSkipAsync(int requested)
        {
            return SkipByReading(requested);
        }

        public override bool CanReset => base.CanReset && _hasher.CanReuseTransform;
        public async override ValueTask ResetAsync()
        {
            await base.ResetAsync();
            _hasher.Initialize();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
                _hasher.Dispose();
        }

        protected override ValueTask DisposeAsyncCore()
        {
            _hasher.Dispose();
            return base.DisposeAsyncCore();
        }

        public byte[]? HashResult => _result;
    }
}
