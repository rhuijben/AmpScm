using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Git.Objects
{
    internal sealed class CommitGraphRepository : GitObjectRepository
    {
        readonly string _fileName;
        GitIdType _idType;
        FileStream? _fs;

        public CommitGraphRepository(GitRepository repository, string chainFile) : base(repository, "CommitGraph:"+chainFile)
        {
            _fileName = chainFile ?? throw new ArgumentNullException(nameof(chainFile));
        }

        public override async IAsyncEnumerable<TGitObject> GetAll<TGitObject>(HashSet<GitId> alreadyReturned)
        {
            if (!typeof(TGitObject).IsAssignableFrom(typeof(GitCommit)))
                yield break;

            await Init().ConfigureAwait(false);

            for (uint i = 0; i < (_fanOut?[255] ?? 0); i++)
            {
                var oid = GetOid(i);

                yield return (TGitObject)(object)new GitCommit(Repository, await Repository.ObjectRepository.ResolveByOid(oid).ConfigureAwait(false)!, oid);
            }
        }

        private GitId GetOid(uint i)
        {
            int hashLength = GitId.HashLength(_idType);
            byte[] oidData = new byte[hashLength];

            if (ReadFromChunk("OIDL", i * hashLength, oidData) != hashLength)
                throw new InvalidOperationException();

            return new GitId(_idType, oidData);
        }

        [DebuggerDisplay("{Type, Nq}, Length={Length}")]
        struct Chunk
        {
            public string? Name;
            public long Position;
            public long Length;
        }

        Chunk[]? _chunks;
        private uint[]? _fanOut;

        async ValueTask Init()
        {
            if (_fanOut is not null)
                return;

            await Task.Yield();
            _fs ??= File.OpenRead(_fileName);
            _fs.Seek(0, SeekOrigin.Begin);
            var headerBuffer = new byte[8];
            if (await _fs.ReadAsync(headerBuffer, 0, headerBuffer.Length, CancellationToken.None).ConfigureAwait(false) != headerBuffer.Length)
                return;

            if (!new byte[] { (byte)'C', (byte)'G', (byte)'P', (byte)'H', 1 /* version */ }.SequenceEqual(headerBuffer.Take(5)))
                return;

            _idType = (GitIdType)headerBuffer[5];
            int chunkCount = headerBuffer[6];
            int baseCommitGraphs = headerBuffer[7];

            var chunkTable = new byte[(chunkCount + 1) * (4 + sizeof(long))];

            if (await _fs.ReadAsync(chunkTable, 0, chunkTable.Length, CancellationToken.None).ConfigureAwait(false) != chunkTable.Length)
                return;

            _chunks = Enumerable.Range(0, chunkCount + 1).Select(i => new Chunk
            {
                Name = (i < chunkCount) ? Encoding.ASCII.GetString(chunkTable, 12 * i, 4) : null,
                Position = NetBitConverter.ToInt64(chunkTable, 12 * i + 4)
            }).ToArray();

            for (int i = 0; i < chunkCount; i++)
            {
                _chunks[i].Length = _chunks[i + 1].Position - _chunks[i].Position;
            }

            byte[] fanOut = new byte[256 * sizeof(int)];

            if (ReadFromChunk("OIDF", 0, fanOut) != fanOut.Length)
                return;

            _fanOut = Enumerable.Range(0, 256).Select(i => NetBitConverter.ToUInt32(fanOut, sizeof(int) * i)).ToArray();
        }


        private int ReadFromChunk(string chunkType, long position, byte[] buffer)
        {
            return ReadFromChunk(chunkType, position, buffer, 0, buffer.Length);
        }

        private int ReadFromChunk(string chunkType, long position, byte[] fanOut, int offset, int length)
        {
            if (_chunks == null || _fs == null)
                return 0;

            Chunk? ch = null;
            foreach (var c in _chunks)
            {
                if (c.Name == chunkType)
                {
                    ch = c;
                    break;
                }
            }
            if (ch == null)
                return 0;

            _fs.Seek(ch.Value.Position + position, SeekOrigin.Begin);

            int requested = (int)Math.Min(length, ch.Value.Length - position);

            return _fs.Read(fanOut, offset, requested);
        }

        internal override async ValueTask<IGitCommitGraphInfo?> GetCommitInfo(GitId oid)
        {
            await Init().ConfigureAwait(false);

            if (TryFindId(oid, out var index))
            {
                int hashLength = GitId.HashLength(_idType);
                int commitDataSz = hashLength + 2 * sizeof(uint) + sizeof(ulong);
                byte[] commitData = new byte[commitDataSz];

                if (ReadFromChunk("CDAT", index * commitDataSz, commitData) != commitDataSz)
                    return null;

                // commitData now contains the root hash, 2 parent indexes and the topological level
                uint parent0 = NetBitConverter.ToUInt32(commitData, hashLength);
                uint parent1 = NetBitConverter.ToUInt32(commitData, hashLength + sizeof(uint));
                ulong chainLevel = NetBitConverter.ToUInt64(commitData, hashLength + 2 * sizeof(uint));

                GitId[] parents;

                if (parent0 == 0x70000000)
                    parents = Array.Empty<GitId>();
                else if (parent1 == 0x70000000)
                    parents = new[] { GetOid(parent0) };
                else if (parent1 >= 0x80000000)
                {
                    var extraParents = new byte[sizeof(uint) * 256];
                    int len = ReadFromChunk("EDGE", 4 * (parent1 & 0x7FFFFFFF), extraParents) / 4;

                    if (len == 0 || len >= 256)
                        return null; // Handle as if not exists in chain. Should never happen

                    int? stopAfter = null;
                    parents = new[] { parent0 }.Concat(
                        Enumerable.Range(0, len)
                            .Select(i => NetBitConverter.ToUInt32(extraParents, i * sizeof(uint)))
                            .TakeWhile((v, i) => { if (i > stopAfter) return false; else if ((v & 0x80000000) != 0) { stopAfter = i; }; return true; }))
                            .Select(v => GetOid(v & 0x7FFFFFFF)).ToArray();
                }
                else
                    parents = new[] { GetOid(parent0), GetOid(parent1) };

                return new GitCommitGraphInfo(parents, chainLevel);
            }

            return await base.GetCommitInfo(oid).ConfigureAwait(false);
        }

        private bool TryFindId(GitId oid, out uint index)
        {
            if (_fanOut == null)
            {
                index = 0;
                return false;
            }

            uint first = (oid[0] == 0) ? 0 : _fanOut[oid[0] - 1];
            uint count = _fanOut[oid[0]];

            uint c = count;

            while (first < c - 1)
            {
                uint mid = (first + c) / 2;

                var check = GetOid(mid);

                int n = oid.HashCompare(check);

                if (n == 0)
                {
                    index = (uint)mid;
                    return true;
                }
                else if (n < 0)
                    c = mid;
                else
                    first = mid + 1;
            }

            if (first >= count)
            {
                index = 0;
                return false;
            }

            var check2 = GetOid(first);
            index = (uint)first;

            return oid.HashCompare(check2) == 0;
        }

        internal override bool ProvidesGetObject => false;
    }
}
