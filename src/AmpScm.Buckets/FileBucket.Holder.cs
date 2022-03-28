using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace AmpScm.Buckets
{
    public partial class FileBucket
    {
        sealed class FileHolder
        {
            readonly string _path;
            readonly Stack<FileStream> _keep = new Stack<FileStream>();
            readonly FileStream _primary;
            int _nRefs;
            long? _length;

            IntPtr _ovlBase;
            Stack<IntPtr>? _ovls;

            public FileHolder(FileStream primary, string path)
            {
                _primary = primary ?? throw new ArgumentNullException(nameof(primary));
                _path = path ?? throw new ArgumentNullException(nameof(path));

                if (primary.IsAsync)
                    _keep.Push(primary);

                PrepareAsync();
            }

            void Dispose()
            {
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                    DisposeWindows(true);

                GC.SuppressFinalize(this);
            }

            ~FileHolder()
            {
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                    DisposeWindows(false);
            }

            private void PrepareAsync()
            {
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
#pragma warning disable CA1416 // Validate platform compatibility
                    PrepareWinAsync();
#pragma warning restore CA1416 // Validate platform compatibility
            }

#if NET5_0_OR_GREATER
            [SupportedOSPlatform("windows")]
#endif
            void PrepareWinAsync()
            {
                int ovlSize = (Marshal.SizeOf<NativeOverlapped>() + 15) & ~0xF;

                _ovlBase = Marshal.AllocCoTaskMem(16 * ovlSize);

                _ovls = new Stack<IntPtr>();

                for (int i = 0; i < 16; i++)
                    _ovls.Push((IntPtr)((ulong)_ovlBase + (ulong)(i * ovlSize)));
            }

#if NET5_0_OR_GREATER

            [SupportedOSPlatform("windows")]
#endif
            void DisposeWindows(bool disposing)
            {
                if (_ovls?.Count == 16)
                    Marshal.FreeCoTaskMem(_ovlBase);
            }

            static class NativeMethods
            {
                public delegate void IOCompletionCallback(uint errorCode, uint numBytes, IntPtr pOVERLAP);
                [DllImport("kernel32.dll", SetLastError =true)]
                [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
                public static extern bool ReadFileEx(SafeFileHandle hFile, [Out] byte[] lpBuffer, uint nNumberOfBytesToRead, [In] IntPtr lpOverlapped, IOCompletionCallback lpCompletionRoutine);


                [DllImport("kernel32.dll", SetLastError = true)]
                [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
                public static extern bool GetOverlappedResult(SafeFileHandle hFile, [In] IntPtr lpOverlapped, out uint lpNumberOfBytesTransferred, bool bWait);
            }

#if NET5_0_OR_GREATER
            [SupportedOSPlatform("windows")]
#endif
            private bool TryWinReadAtSync(out ValueTask<int> vt, long readPos, byte[] buffer, int readLen)
            {
                IntPtr v;
                lock (_ovls!)
                {
                    if (_ovls.Count == 0)
                    {
                        vt = default;
                        return false;
                    }

                    v = _ovls.Pop();
                }


                TaskCompletionSource<int> tcs = new TaskCompletionSource<int>();

                NativeOverlapped nop = default;
                nop.OffsetLow = (int)((uint)readPos & uint.MaxValue);
                nop.OffsetHigh = (int)(uint)(readPos >> 32);

                Marshal.StructureToPtr(nop, v, false);

                if (NativeMethods.ReadFileEx(_primary.SafeFileHandle, buffer, (uint)readLen, v,
                    (errorCode, numBytes, pOverlap) =>
                    {
                        uint bytesRead;
                        if (NativeMethods.GetOverlappedResult(_primary.SafeFileHandle, v, out bytesRead, true))
                        {
                            tcs.SetResult((int)bytesRead);
                        }
                        else
                        {
                            tcs.SetException(Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error())!);
                        }

                        lock (_ovls)
                        {
                            _ovls.Push(v);
                        }
                    }))
                {
                    vt = new ValueTask<int>(tcs.Task);
                    return true;
                }
                else
                {
                    lock (_ovls)
                    {
                        _ovls.Push(v);
                    }
                    throw Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error());

                }
            }


            public void AddRef()
            {
                _nRefs++;
            }

            public void Release()
            {
                _nRefs--;

                if (_nRefs >= 0)
                {
                    while (_keep.Count > 0)
                    {
                        var r = _keep.Pop();
#if NET6_0_OR_GREATER
                        if (!r.IsAsync)
#endif
                        {
                            r.Dispose();
                        }
                    }
                    _primary.Dispose();
                }
                else
                    Dispose();
            }

            public ValueTask<int> ReadAtAsync(long readPos, byte[] buffer, int readLen)
            {
#pragma warning disable CA1416 // Validate platform compatibility
                if (_ovls is not null && TryWinReadAtSync(out var vt, readPos, buffer, readLen))
                    return vt;
#pragma warning restore CA1416 // Validate platform compatibility
                if (_primary.IsAsync)
                    return TrueReadAtAsync(readPos, buffer, readLen);
                else
                {
                    using (GetFileStream(out var p))
                    {
                        if (p.Position != readPos)
                            p.Position = readPos;

#pragma warning disable CA1849 // Call async methods when in an async method
                        int r = p.Read(buffer, 0, readLen);
#pragma warning restore CA1849 // Call async methods when in an async method

                        return new ValueTask<int>(r);
                    }
                }
            }

            public async ValueTask<int> TrueReadAtAsync(long readPos, byte[] buffer, int readLen)
            {
                bool primary = false;
                try
                {
                    using (GetFileStream(out var p))
                    {
                        primary = (p == _primary);
                        if (p.Position != readPos)
                            p.Position = readPos;

#if !NETFRAMEWORK
                        var r = await p.ReadAsync(buffer.AsMemory(0, readLen)).ConfigureAwait(false);
#else
                        var r = await p.ReadAsync(buffer, 0, readLen).ConfigureAwait(false);
#endif
                        return r;
                    }
                }
                catch (Exception e) when (primary)
                {
                    throw new BucketException("Error reading primary", e);
                }
            }

            Returner GetFileStream(out FileStream f)
            {
                lock (_keep)
                {
                    FileStream p = _keep.Count > 0 ? _keep.Pop() : NewStream();
                    var r = new Returner(this, p); // Do this before setting the out argument
                    f = p;

                    return r;
                }
            }

            private FileStream NewStream()
            {
#if NET6_0_OR_GREATER
                if (_primary.IsAsync)
                    return new FileStream(_primary.SafeFileHandle, FileAccess.Read, 4096, true);
#endif
                return new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 4096, true);
            }

            private void Return(FileStream f)
            {
                if (f == null)
                    throw new ArgumentNullException(nameof(f));

                lock (_keep)
                {
                    if (_keep.Count > 4 && (f != _primary))
                    {
#if NET6_0_OR_GREATER
                        if (_primary.IsAsync) // All file instances share the same handle
                        {
#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize
                            GC.SuppressFinalize(f);
#pragma warning restore CA1816 // Dispose methods should call SuppressFinalize
                        }
                        else
#endif
                        {

                            f.Dispose();
                        }
                    }
                    else
                        _keep.Push(f);
                }
            }

            public long Length => _length ?? (_length = _primary.Length).Value;


            sealed class Returner : IDisposable
            {
                FileHolder? _fh;
                FileStream _fs;

                public Returner(FileHolder fh, FileStream fs)
                {
                    _fh = fh;
                    _fs = fs;
                }

                public void Dispose()
                {
                    _fh?.Return(_fs);
                    _fh = null;
                }
            }
        }
    }
}
