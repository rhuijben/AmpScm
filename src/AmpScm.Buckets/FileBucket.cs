using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace AmpScm.Buckets
{
    public class FileBucket : Bucket
    {
        FileHolder _holder;
        byte[] _buffer;
        int _size;
        int _pos;
        long _filePos;
        long _bufStart;
        readonly int _chunkSizeMinus1;

        private FileBucket(FileHolder holder, int bufferSize = 8192, int chunkSize = 2048)
        {
            _holder = holder ?? throw new ArgumentNullException(nameof(holder));
            _holder.AddRef();
            _buffer = new byte[bufferSize];
            _chunkSizeMinus1 = chunkSize - 1;
            _bufStart = -bufferSize;
        }

        public override ValueTask<long?> ReadRemainingBytesAsync()
        {
            return new ValueTask<long?>(_holder.Length - _filePos);
        }

        public override string Name => "File";

        public override long? Position => _filePos;

        public override ValueTask<BucketBytes> PeekAsync()
        {
            if (_pos < _size)
                return new BucketBytes(_buffer, _pos, _size - _pos);
            else
                return Bucket.EmptyTask;
        }
        public override bool CanReset => true;

        public override ValueTask ResetAsync()
        {
            _pos = _size;
            _filePos = 0;

            return default;
        }

        public override ValueTask<Bucket> DuplicateAsync(bool reset)
        {
            FileBucket fbNew = new FileBucket(_holder, _buffer.Length, _chunkSizeMinus1 + 1);

            if (reset)
                fbNew._filePos = 0;
            else
                fbNew._filePos = _filePos;

            return new ValueTask<Bucket>(fbNew);
        }

        const int MinCache = 16; // Only use the existing cache instead of seek when at least this many bytes are available

        public async override ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            if (requested <= 0)
                throw new ArgumentOutOfRangeException(nameof(requested));

            if (_pos < _size)
            {
                BucketBytes data = new BucketBytes(_buffer, _pos, Math.Min(requested, _size - _pos));
                _pos += data.Length;
                _filePos += data.Length;
                return data;
            }

            long basePos = _filePos & ~_chunkSizeMinus1; // Current position round back to chunk
            int extra = (int)(_filePos - basePos); // Position in chunk

            int readLen = (requested + extra + _chunkSizeMinus1) & ~_chunkSizeMinus1;

            if (readLen > _buffer.Length)
                readLen = _buffer.Length;


            if (_bufStart != basePos || readLen > _size)
            {
                if (_filePos < _bufStart + _size - Math.Min(requested, MinCache) && _filePos >= _bufStart)
                {
                    // We still have the requested data
                }
                else
                {
                    _size = await _holder.ReadAtAsync(basePos, _buffer, readLen);
                    _bufStart = basePos;
                }
            }
            _pos = (int)(_filePos - _bufStart);

            if (_size == 0 || _pos == _size)
            {
                _pos = _size;
                return BucketBytes.Eof;
            }

            BucketBytes result = new BucketBytes(_buffer, _pos, Math.Min(requested, _size - _pos));
            _pos += result.Length;
            _filePos += result.Length;

            System.Diagnostics.Debug.Assert(result.Length > 0);

            return result;
        }

        public override ValueTask<int> ReadSkipAsync(int requested)
        {
            if (requested <= 0)
                throw new ArgumentOutOfRangeException(nameof(requested));

            if (_size - _pos > requested)
                return base.ReadSkipAsync(requested);
            else
            {
                _pos = _size;

                long newPos = Math.Min(_filePos + requested, _holder.Length);

                int skipped = (int)(newPos - _filePos);
                _filePos = newPos;
                return new ValueTask<int>(skipped);
            }
        }

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

                        int r = p.Read(buffer, 0, readLen);

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

                        var r = await p.ReadAsync(buffer, 0, readLen);
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
                            GC.SuppressFinalize(f);
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

        public static FileBucket OpenRead(string path, bool forAsync)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            FileStream primary;

            if (forAsync)
                primary = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 4096, FileOptions.Asynchronous);
            else
                primary = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 4096);

            FileHolder fh = new FileHolder(primary, path);

            return new FileBucket(fh);
        }

        public static FileBucket OpenRead(string path)
        {
            return OpenRead(path, true);
        }

        //public static FileBucket OpenRead(FileStream from)
        //{
        //    if (from == null)
        //        throw new ArgumentNullException(nameof(from));
        //    else if (!from.CanRead)
        //        throw new ArgumentException("Unreadable stream", nameof(from));
        //
        //    FileHolder fh = new FileHolder(from, null);
        //
        //    return new FileBucket(fh);
        //}
    }
}
