using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Amp.Buckets
{
    [DebuggerDisplay("{Name}: BucketCount={BucketCount}, Current={CurrentBucket}, Position={Position}")]
    public class AggregateBucket : Bucket, IBucketAggregation
    {
        Bucket?[] _buckets;
        int _n;
        bool _keepOpen;
        long _position;

        public AggregateBucket(params Bucket[] items)
        {
            _buckets = items ?? Array.Empty<Bucket>();
        }

        public AggregateBucket(bool keepOpen, params Bucket[] items)
            : this(items)
        {
            _keepOpen = keepOpen;
        }

        public Bucket Append(Bucket bucket)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));

            int nShrink = _keepOpen ? 0 : _n;

            var newBuckets = new Bucket[_buckets.Length - nShrink + 1];
            Array.Copy(_buckets, _n, newBuckets, 0, _buckets.Length - nShrink);
            _buckets = newBuckets;
            newBuckets[newBuckets.Length - 1] = bucket;
            _n -= nShrink;

            return this;
        }

        public Bucket Prepend(Bucket bucket)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));

            if (!_keepOpen && _n > 0)
                _buckets[--_n] = bucket;
            else if (_n > 0)
                throw new InvalidOperationException();
            {
                var newBuckets = new Bucket[_buckets.Length + 1];
                Array.Copy(_buckets, _n, newBuckets, 1, _buckets.Length);
                newBuckets[0] = bucket;
            }
            return this;
        }

        public override bool CanReset => _keepOpen && _buckets.All(x => x!.CanReset);

        public override async ValueTask ResetAsync()
        {
            if (!_keepOpen)
                throw new InvalidOperationException();

            if (_n >= _buckets.Length)
                _n = _buckets.Length - 1;

            while (_n >= 0)
            {
                await _buckets[_n]!.ResetAsync();
                _n--;
            }
            _n = 0;
            _position = 0;
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = -1)
        {
            while (_n < _buckets.Length)
            {
                var r = await _buckets[_n]!.ReadAsync(requested);

                if (!r.IsEof)
                {
                    if (r.Length == 0)
                        throw new InvalidOperationException("Got 0 byte read");

                    _position += r.Length;

                    return r;
                }

                if (!_keepOpen)
                {
                    await _buckets[_n]!.DisposeAsync();
                    _buckets[_n] = null;
                }

                _n++;
            }
            if (!_keepOpen)
            {
                _buckets = Array.Empty<Bucket>();
                _n = 0;
            }
            return BucketBytes.Eof;
        }

        public override ValueTask<int> ReadSkipAsync(int requested)
        {
            return base.ReadSkipAsync(requested);
        }

        public override async ValueTask<long?> ReadRemainingBytesAsync()
        {
            int n = _n;
            long remaining = 0;

            while (n < _buckets.Length)
            {
                var r = await _buckets[n]!.ReadRemainingBytesAsync();

                if (!r.HasValue)
                    return null;

                remaining += r.Value;
                n++;
            }
            return remaining;
        }

        public override ValueTask<BucketBytes> PeekAsync()
        {
            int n = _n;
            while (n < _buckets.Length)
            {
                var v = _buckets[n]!.PeekAsync();

                if (!v.IsCompleted || !v.Result.IsEof)
                    return v;

                n++; // Peek next bucket, but do not dispose. We can do that in the next read
            }

            return EofTask;
        }

        protected override async ValueTask DisposeAsyncCore()
        {
            while (_n < _buckets.Length)
            {
                if (_buckets[_n] != null)
                {
                    await _buckets[_n]!.DisposeAsync();
                }
                _buckets[_n++] = null;
            }

            _buckets = Array.Empty<Bucket>();
            _n = 0;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                for (int i = 0; i < _buckets.Length; i++)
                {
                    _buckets[i]?.Dispose();
                    _buckets[i] = null;
                }

                _buckets = Array.Empty<Bucket>();
                _n = 0;
            }
            base.Dispose(disposing);
        }

        public override long? Position
        {
            get => _position;
        }

        public async override ValueTask<Bucket> DuplicateAsync(bool reset)
        {
            if (!_keepOpen)
                throw new NotSupportedException();
            else if (reset && !CanReset)
                throw new InvalidOperationException();

            var newBuckets = new List<Bucket>();

            foreach (var v in _buckets)
                newBuckets.Add(await v!.DuplicateAsync(reset));

            var ab = new AggregateBucket(true, newBuckets.ToArray());
            if (!reset)
                ab._position = _position;
            return ab;
        }

        #region DEBUG INFO
        int BucketCount => _buckets.Length - _n;
        Bucket? CurrentBucket => _buckets[_n];
        #endregion
    }
}
