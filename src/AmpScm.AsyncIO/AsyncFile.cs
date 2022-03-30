using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace AmpScm.AsyncIO
{
    /// <summary>
    /// Asynchronous file IO, allowing multiple concurrent readers
    /// </summary>
    public sealed class AsyncFile : IDisposable
    {
        FileStream? _fs;
        SafeFileHandle _handle;
        private bool disposedValue;
        Action? _disposers;
        Action? _next;
        readonly Queue<IntPtr> _overlappeds = new Queue<IntPtr>();
        bool _busy;

        public AsyncFile(string path, FileMode mode)
            : this(path, mode, FileAccess.ReadWrite)
        {
        }

        public AsyncFile(string path, FileMode mode, FileAccess access)
            : this(path, mode, access, (mode, access) switch
            {
                (FileMode.Create, _) => FileShare.None,
                (FileMode.CreateNew, _) => FileShare.None,
                (FileMode.Truncate, _) => FileShare.None,
                (_, FileAccess.Read) => FileShare.Read,
                (_, FileAccess.ReadWrite) => FileShare.Read,
                (_, FileAccess.Write) => FileShare.None,
                _ => FileShare.None
            })
        {
        }

        FileStream? _noUse;
        public AsyncFile(string path, FileMode mode, FileAccess access, FileShare share)
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                _fs = new FileStream(path, mode, access, share, 4096, true);
                _handle = _fs.SafeFileHandle;
            }
            else
            {
                _handle = NativeMethods.CreateFileW(path, access switch
                {
                    FileAccess.Read => 0x80000000 /* GENERIC_READ */,
                    FileAccess.Write => 0x40000000 /* GENERIC_WRITE */,
                    FileAccess.ReadWrite => 0x80000000 | 0x40000000
                }
                    , share, IntPtr.Zero, mode,
                    (FileAttributes)(0x80 /* Normal attributes */ | 0x40000000 /* Overlapped */), IntPtr.Zero);

                if (_handle.IsInvalid)
                    throw Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()) ?? new InvalidOperationException();
            }
        }

        public ValueTask<int> ReadAsync(long offset, byte[] buffer, int index, int length)
        {
            if (_fs is not null)
                return ReadOnStreamAsync(offset, buffer, index, length);
            else
                return ReadOnHandleAsync(offset, buffer, index, length);
        }

        private ValueTask<int> ReadOnHandleAsync(long offset, byte[] buffer, int index, int length)
        {
            TaskCompletionSource<int> taskCompletionSource = new TaskCompletionSource<int>();
            IntPtr ovl;
            lock (_overlappeds)
            {
#if NET5_0_OR_GREATER
                if (_overlappeds.TryDequeue(out var ovt))
#else
                if (_overlappeds.Count > 0 && _overlappeds.Dequeue() is IntPtr ovt)
#endif
                {
                    ovl = ovt;
                }
                else
                {
                    int sz = Marshal.SizeOf<NativeOverlapped>();
                    IntPtr p = Marshal.AllocCoTaskMem(Marshal.SizeOf<NativeOverlapped>() * 16);

                    if (p == IntPtr.Zero)
                        throw new InvalidOperationException();

                    _disposers += () => Marshal.FreeCoTaskMem(p);
                    for (int i = 1; i < 16; i++)
                    {
                        _overlappeds.Enqueue((IntPtr)((long)p + i * sz));
                    }
                    ovl = p;
                }
            }

            NativeOverlapped nol = default;
            nol.OffsetLow = (int)(offset & uint.MaxValue);
            nol.OffsetHigh = (int)(offset >> 32);

            Marshal.StructureToPtr(nol, ovl, false);

            GCHandle pin = GCHandle.Alloc(buffer, GCHandleType.Pinned);

            try
            {
                TaskCompletionSource<int> tcs = new TaskCompletionSource<int>();
                if (NativeMethods.ReadFileEx(_handle, buffer, length, ovl, (x, y, z) => {
                    pin.Free();
                    if (x == 0)
                        tcs.SetResult((int)y);
                    else
                        tcs.SetException(new InvalidOperationException());
                    }))
                {


                    return new ValueTask<int>(tcs.Task);
                }
                else
                {
                    throw Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()) ?? new InvalidOperationException();
                }
            }
            catch
            {
                pin.Free();
                throw;
            }
        }

        private ValueTask<int> ReadOnStreamAsync(long offset, byte[] buffer, int index, int length)
        {
            bool busy;
            lock (_fs!)
            {
                busy = _busy;
                if (!busy)
                    _busy = true;
                else
                {
                    TaskCompletionSource<int> taskCompletionSource = new TaskCompletionSource<int>();
                    var next = _next;

                    _next = () =>
                    {
                        _fs.Position = offset;
                        _fs.ReadAsync(buffer, index, length).ContinueWith(i =>
                        {
                            if (i.Status == TaskStatus.RanToCompletion)
                            {
                                taskCompletionSource.SetResult(i.Result);
                            }
                            else
                            {
                                taskCompletionSource.SetException(i.Exception ?? new AggregateException(new InvalidOperationException()));
                            }

                            if (next is not null)
                                next();
                            else
                                _next = null;
                        });
                    };
                    return new ValueTask<int>(taskCompletionSource.Task);
                }
            }

            return FsReadOnStreamAsync(offset, buffer, index, length);
        }

        async ValueTask<int> FsReadOnStreamAsync(long offset, byte[] buffer, int index, int length)
        {
            _fs!.Position = offset;
            try
            {
                return await _fs.ReadAsync(buffer, index, length);
            }
            finally
            {
                Action? next = null;
                lock (_fs)
                {
                    if (_next is null)
                        _busy = false;
                    else
                    {
                        next = _next;
                        _next = null;
                    }
                }

                next?.Invoke();
            }
        }

        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _fs?.Dispose();
                    _handle.Dispose();
                    _disposers?.Invoke();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~AsyncFile()
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
