using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Amp.Buckets
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
        bool _isChild;

        private FileBucket(FileHolder holder, int bufferSize = 8192, int chunkSize = 1024)
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

            fbNew._isChild = true;

            return new ValueTask<Bucket>(fbNew);
        }

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
                _size = await _holder.ReadAtAsync(basePos, _buffer, readLen);
                _bufStart = basePos;
            }

            if (_size == 0)
            {
                _pos = _size;
                _bufStart = -_buffer.Length;
                return BucketBytes.Eof;

            }
            _pos = (int)(_filePos - basePos);


            BucketBytes result = new BucketBytes(_buffer, _pos, Math.Min(requested, _size - _pos));
            _pos += result.Length;
            _filePos += result.Length;

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
            readonly string? _path;
            readonly Stack<FileStream> _keep = new Stack<FileStream>();
            readonly FileStream _primary;
            int _nRefs;
            long? _length;

            public FileHolder(FileStream primary, string? path)
            {
                _primary = primary ?? throw new ArgumentNullException(nameof(primary));
                _path = path;

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
                        _keep.Pop().Dispose();
                    }
                    _primary.Dispose();
                }
            }

            public ValueTask<int> ReadAtAsync(long readPos, byte[] buffer, int readLen)
            {
                if (_primary.Position != readPos)
                    _primary.Position = readPos;

                int r = _primary.Read(buffer, 0, readLen);

                return new ValueTask<int>(r);
            }

            public long Length => _length ?? (_length = _primary.Length).Value;
        }

        public static FileBucket OpenRead(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            FileStream primary = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 4096, true);

            FileHolder fh = new FileHolder(primary, path);

            return new FileBucket(fh);
        }

        public static FileBucket OpenRead(FileStream from)
        {
            if (from == null)
                throw new ArgumentNullException(nameof(from));
            else if (!from.CanRead)
                throw new ArgumentException("Unreadable stream", nameof(from));

            FileHolder fh = new FileHolder(from, null);

            return new FileBucket(fh);
        }
    }
}
