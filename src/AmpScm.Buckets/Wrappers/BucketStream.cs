using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;

namespace AmpScm.Buckets.Wrappers
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2012:Use ValueTasks correctly", Justification = "<Pending>")]
    internal partial class BucketStream : Stream
    {
        bool _gotLength;
        long _length;

        public BucketStream(Bucket bucket)
        {
            Bucket = bucket ?? throw new ArgumentNullException(nameof(bucket));
        }

        public Bucket Bucket { get; }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                    Bucket.Dispose();
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

#if !NETFRAMEWORK
        public override async ValueTask DisposeAsync()
        {
            await Bucket.DisposeAsync().ConfigureAwait(false);
            await base.DisposeAsync().ConfigureAwait(false);
        }
#endif

        public override bool CanRead => true;

        public override bool CanSeek => Bucket.CanReset;

        public override bool CanWrite => false;

        public override long Length
        {
            get
            {
                if (!_gotLength)
                {
                    _gotLength = true;

                    var p = Bucket.Position;

                    if (!p.HasValue)
                        return -1L;

                    var r = Bucket.ReadRemainingBytesAsync().Result; // BAD async

                    if (r.HasValue)
                        _length = r.Value + p.Value;
                }
                return _length;
            }
        }

        public override long Position { get => Bucket.Position ?? 0L; set => Seek(value, SeekOrigin.Begin); }

        public override void Flush()
        {
            //throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return Read(new Span<byte>(buffer, offset, count));
        }


#if !NETFRAMEWORK
        public override int Read(Span<byte> buffer)
#else
        internal virtual int Read(Span<byte> buffer)
#endif

        {
            var v = Bucket.ReadAsync(buffer.Length);

            if (!v.IsCompleted)
                v.AsTask().Wait();

            var r = v.Result;

            if (r.IsEof)
                return 0;

            r.Span.CopyTo(buffer);
            return r.Length;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var r = await Bucket.ReadAsync(count).ConfigureAwait(false);

            if (r.IsEof)
                return 0;

            r.CopyTo(new Memory<byte>(buffer, offset, r.Length));
            return r.Length;
        }

#if !NETFRAMEWORK
#pragma warning disable RS0027 // Public API with optional parameter(s) should have the most parameters amongst its public overloads
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
#pragma warning restore RS0027 // Public API with optional parameter(s) should have the most parameters amongst its public overloads
        {
            var r = await Bucket.ReadAsync(buffer.Length).ConfigureAwait(false);

            if (r.IsEof)
                return 0;

            r.CopyTo(buffer);
            return r.Length;
        }
#endif

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
        {
            Debug.WriteLine("BeginRead");
            var task = ReadAsync(buffer, offset, count);

            var tcs = new TaskCompletionSource<int>(state);
            task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                    tcs.TrySetException(t.Exception!.InnerExceptions);
                else if (t.IsCanceled)
                    tcs.TrySetCanceled();
                else
                    tcs.TrySetResult(t.Result);

                if (callback != null)
                    callback(tcs.Task);
            }, TaskScheduler.Default);

            return tcs.Task;
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            Debug.WriteLine("EndRead");
            return ((Task<int>)asyncResult).Result;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new InvalidOperationException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new InvalidOperationException();
        }
    }
}
