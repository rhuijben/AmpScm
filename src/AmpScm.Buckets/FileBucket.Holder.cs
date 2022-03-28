using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

            public FileHolder(FileStream primary, string path)
            {
                _primary = primary ?? throw new ArgumentNullException(nameof(primary));
                _path = path ?? throw new ArgumentNullException(nameof(path));

                if (primary.IsAsync)
                    _keep.Push(primary);
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
            }

            public ValueTask<int> ReadAtAsync(long readPos, byte[] buffer, int readLen)
            {
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
