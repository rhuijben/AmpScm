﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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
            SafeFileHandle _handle;
            int _nRefs;
            long? _length;
            bool _asyncWin;
            Queue<IntPtr> _overlappeds;
            Action _disposers;

            public FileHolder(FileStream primary, string path)
            {
                _primary = primary ?? throw new ArgumentNullException(nameof(primary));
                _path = path ?? throw new ArgumentNullException(nameof(path));

                if (primary.IsAsync)
                    _keep.Push(primary);

                _disposers = _primary.Dispose;
                _overlappeds = default!;
                _handle = primary.SafeFileHandle;
            }

            public FileHolder(SafeFileHandle handle, string path)
            {
                if (handle?.IsInvalid ?? true)
                    throw new ArgumentNullException(nameof(handle));
                if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                    throw new InvalidOperationException("Only supported on Windows at this time");

                _primary = default!;
                _handle = handle;
                _path = path ?? throw new ArgumentNullException(nameof(path));
                _asyncWin = true;
                _overlappeds = new Queue<IntPtr>();

                _disposers = _handle.Dispose;
                _handle = handle;

                _length = NativeMethods.GetFileSize(_handle);
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
                    _disposers.Invoke();
                }
            }

            public ValueTask<int> ReadAtAsync(long readPos, byte[] buffer, int readLen)
            {
                if (readLen <= 0)
                    throw new ArgumentOutOfRangeException(nameof(readLen));
                else if (readPos > 0 && readPos >= Length)
                    return new ValueTask<int>(0);

                if (_asyncWin)
                    return AsyncWinReadAsync(readPos, buffer, readLen);
                else if (_primary.IsAsync)
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

            public ValueTask<int> AsyncWinReadAsync(long readPos, byte[] buffer, int readLen)
            {
                IntPtr ovl;

                if (readLen < 1)
                    throw new ArgumentOutOfRangeException();

                long fp = readPos + readLen;
                if (fp > _length!.Value)
                {
                    long rl = (_length.Value - readPos);

                    if (rl < 1)
                        return new ValueTask<int>(0);
                    readLen = (int)rl;
                }

                lock (_overlappeds)
                {
                    if (_overlappeds.Count > 0)
                        ovl = _overlappeds.Dequeue();
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
                        ovl = p; // And keep the last one
                    }
                }

                NativeOverlapped nol = default;
                nol.OffsetLow = (int)(readPos & uint.MaxValue);
                nol.OffsetHigh = (int)(readPos >> 32);

                Marshal.StructureToPtr(nol, ovl, false);

                GCHandle pin = GCHandle.Alloc(buffer, GCHandleType.Pinned);

                try
                {
                    TaskCompletionSource<int> tcs = new TaskCompletionSource<int>();

                    NativeMethods.IOCompletionCallback cb = (x, y, z) =>
                    {
                        pin.Free();

                        lock (_overlappeds)
                            _overlappeds.Enqueue(ovl);

                        if (x == 0)
                            tcs.TrySetResult((int)y);
                        else
                            tcs.SetException(new InvalidOperationException("GetOverlappedResult failed"));
                    };

                    if (NativeMethods.ReadFileEx(_handle, buffer, readLen, ovl, cb))
                    {
                        try
                        {
                            return new ValueTask<int>(tcs.Task);
                        }
                        finally
                        {
                            if (NativeMethods.GetOverlappedResult(_handle, ovl, out var t, false))
                            {
                                tcs.TrySetResult((int)t);
                            }
                        }
                    }
                    else
                    {
                        throw Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()) ?? new BucketException("ReadFileEx failed");
                    }
                }
                catch
                {
                    pin.Free();
                    throw;
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

            public long Length => _length ?? (_length = _primary?.Length).Value;


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

            static class NativeMethods
            {
                [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
                [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
                public static extern SafeFileHandle CreateFileW(
                    [MarshalAs(UnmanagedType.LPWStr)] string filename,
                    uint access,
                    uint share,
                    IntPtr securityAttributes,
                    uint creationDisposition,
                    uint flagsAndAttributes,
                    IntPtr templateFile);

                public delegate void IOCompletionCallback(uint errorCode, uint numBytes, IntPtr lpOverlapped);

                [DllImport("kernel32.dll", SetLastError = true)]
                [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
                public static extern bool ReadFileEx(SafeFileHandle hFile, [Out] byte[] lpBuffer,
                    int nNumberOfBytesToRead, [In] IntPtr lpOverlapped,
                    IOCompletionCallback lpCompletionRoutine);


                [DllImport("kernel32.dll", SetLastError = true)]
                [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
                public static extern bool GetOverlappedResult(SafeFileHandle hFile, IntPtr lpOverlapped, out uint lpNumberOfBytesTransferred, bool bWait);


                [DllImport("kernel32.dll", SetLastError = true)]
                [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
                static extern bool GetFileSizeEx(SafeFileHandle hFile, out ulong size);

                internal static long? GetFileSize(SafeFileHandle handle)
                {
                    if (GetFileSizeEx(handle, out var size))
                        return (long)size;
                    else
                        return null;
                }
            }

            internal static SafeFileHandle OpenAsyncWin32(string path)
            {
#pragma warning disable CA2000 // Dispose objects before losing scope
                SafeFileHandle handle = NativeMethods.CreateFileW(path,
                    access: 0x80000000 /* GENERIC_READ */, // We want to read
                    share: 0x00000004 /* FILE_SHARE_DELETE */ | 0x00000001 /* FILE_SHARE_READ */, // Others can read, delete, rename, but we keep our file open
                    securityAttributes: IntPtr.Zero,
                    creationDisposition: 3 /* OPEN_EXISTING */,
                    0x80 /* Normal attributes */ | 0x40000000 /* Overlapped */,
                    IntPtr.Zero);
#pragma warning restore CA2000 // Dispose objects before losing scope

                if (!handle.IsInvalid)
                    return handle;
                else
                    throw new FileNotFoundException($"Couldn't open {path}");
            }
        }
    }
}
