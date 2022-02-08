using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;

namespace AmpScm.Buckets.Wrappers
{
    internal partial class BucketStream
    {
        public class WithWriter : BucketStream
        {
            IBucketWriter InnerWriter { get; }
            public WithWriter(Bucket bucket, IBucketWriter writer) 
                : base(bucket)
            {
                InnerWriter = writer ?? throw new ArgumentNullException(nameof(writer));
            }

            void DoWriteBucket(Bucket bucket)
            {
                Debug.WriteLine("Writing");
                InnerWriter.Write(bucket);
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                var data = new byte[count];
                Array.Copy(buffer, offset, data, 0, count);

#pragma warning disable CA2000 // Dispose objects before losing scope
                InnerWriter.Write(data.AsBucket());
#pragma warning restore CA2000 // Dispose objects before losing scope
            }

#if !NETFRAMEWORK
            public override void Write(ReadOnlySpan<byte> buffer)
#else
            internal virtual void Write(ReadOnlySpan<byte> buffer)
#endif
            {
                var data = new byte[buffer.Length];
                buffer.CopyTo(data);

#pragma warning disable CA2000 // Dispose objects before losing scope
                DoWriteBucket(data.AsBucket());
#pragma warning restore CA2000 // Dispose objects before losing scope
            }



            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                var data = new byte[count];
                Array.Copy(buffer, offset, data, 0, count);

#pragma warning disable CA2000 // Dispose objects before losing scope
                DoWriteBucket(data.AsBucket());
#pragma warning restore CA2000 // Dispose objects before losing scope

                return Task.CompletedTask;
            }

#if !NETFRAMEWORK
            public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
#else
            internal virtual ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
#endif

            {
#pragma warning disable CA2000 // Dispose objects before losing scope
                DoWriteBucket(buffer.ToArray().AsBucket());
#pragma warning restore CA2000 // Dispose objects before losing scope

                return default;
            }

            public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
            {
                Span<byte> bufferSpan = new Span<byte>(buffer, offset, count);

#pragma warning disable CA2000 // Dispose objects before losing scope
                DoWriteBucket(bufferSpan.ToArray().AsBucket());
#pragma warning restore CA2000 // Dispose objects before losing scope

                IAsyncResult done = new WriteDone { AsyncState = state };

                //if (callback != null)
                //    callback(done);

                return done;
            }

            public override void EndWrite(IAsyncResult asyncResult)
            {
            }

            public override void Flush()
            {
            }

            public override Task FlushAsync(CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public override bool CanWrite => (InnerWriter != null);

            sealed class WriteDone : IAsyncResult
            {
                public object? AsyncState { get; set; }

                public WaitHandle AsyncWaitHandle => throw new InvalidOperationException();

                public bool CompletedSynchronously => true;

                public bool IsCompleted => true;
            }
        }
    }
}
