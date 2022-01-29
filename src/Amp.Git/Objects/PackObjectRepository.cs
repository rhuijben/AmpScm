﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amp.Buckets;
using Amp.Buckets.Git;

namespace Amp.Git.Objects
{
    internal class PackObjectRepository : GitObjectRepository
    {
        readonly string _packFile;
        FileStream? _fIdx;
        Bucket? _fb;
        int _ver;
        uint[]? _fanOut;

        public PackObjectRepository(GitRepository repository, string packFile)
            : base(repository)
        {
            _packFile = packFile ?? throw new ArgumentNullException(nameof(packFile));
        }

        internal static uint ToHost(uint value)
        {
            if (BitConverter.IsLittleEndian)
            {
                // TODO: Optimize
                return BitConverter.ToUInt32(BitConverter.GetBytes(value).Reverse().ToArray(), 0);
            }
            return value;
        }

        void Init()
        {
            if (_ver == 0)
            {
                _fIdx ??= File.OpenRead(Path.ChangeExtension(_packFile, ".idx"));

                byte[] header = new byte[8];
                long fanOutOffset = -1;
                if (8 == _fIdx.Read(header, 0, header.Length))
                {
                    var index = new byte[] { 255, (byte)'t', (byte)'O', (byte)'c', 0, 0, 0, 2 };

                    if (header.SequenceEqual(index))
                    {
                        // We have a v2 header.
                        fanOutOffset = 8;
                        _ver = 2;
                    }
                    else if (header.Take(4).SequenceEqual(index.Take(4)))
                    {
                        // We have an unsupported future header
                        _ver = -1;
                        _fIdx.Dispose();
                        _fIdx = null;
                        return;
                    }
                    else
                    {
                        // We have a v0/v1 header, which is no header
                        fanOutOffset = 0;
                        _ver = 1;
                    }
                }

                if (_fanOut == null && _ver > 0)
                {
                    byte[] fanOut = new byte[4 * 256];

                    _fIdx.Position = fanOutOffset;

                    if (fanOut.Length == _fIdx.Read(fanOut, 0, fanOut.Length))
                    {
                        _fanOut = new uint[256];
                        for (int i = 0; i < 256; i++)
                        {
                            _fanOut[i] = ToHost(BitConverter.ToUInt32(fanOut, i * 4));
                        }
                    }
                }
            }
        }

        private bool TryFindOid(byte[] oids, GitObjectId objectId, out uint index)
        {
            int sz;
            int hashLen = objectId.Hash.Length;

            if (oids.Length == 0)
            {
                index = 0;
                return false;
            }

            if (_ver == 2)
                sz = Repository.InternalConfig.IdBytes;
            else if (_ver == 1)
                sz = Repository.InternalConfig.IdBytes + 4;
            else
            {
                index = 0;
                return false;
            }

            int first = 0, count = oids.Length / sz;
            int c = count;

            while (first < c - 1)
            {
                int mid = (first + c) / 2;

                var check = new Span<byte>(oids, sz * mid, hashLen);
                bool equal = true;

                for (int i = 0; i < hashLen; i++)
                {
                    int n = objectId.Hash[i] - check[i];

                    if (n == 0)
                        continue;

                    equal = false;
                    if (n < 0)
                        c = mid;
                    else
                        first = mid + 1;
                    break;
                }

                if (equal)
                {
                    index = (uint)mid;
                    return true;
                }
            }

            if (first >= count)
            {
                index = 0;
                return false;
            }

            var check2 = new Span<byte>(oids, sz * first, hashLen);
            for (int i = 0; i < objectId.Hash.Length; i++)
            {
                int n = objectId.Hash[i] - check2[i];

                if (n != 0)
                {
                    index = 0;
                    return false;
                }
            }
            index = (uint)first;
            return true;
        }

        private byte[] GetOidArray(uint start, uint count)
        {
            if (_ver == 2)
            {
                int sz = Repository.InternalConfig.IdBytes;
                byte[] data = new byte[sz * count];

                _fIdx!.Position = 8 /* header */ + 256 * 4 /* fanout */ + sz * start;

                if (data.Length != _fIdx.Read(data, 0, data.Length))
                    return Array.Empty<byte>();

                return data;
            }
            else if (_ver == 1)
            {
                int sz = Repository.InternalConfig.IdBytes + 4;
                byte[] data = new byte[sz * count];

                _fIdx!.Position = 256 * 4 /* fanout */ + sz * start;

                if (data.Length != _fIdx.Read(data, 0, data.Length))
                    return Array.Empty<byte>();

                return data;
            }
            else
                return Array.Empty<byte>();
        }

        private byte[] GetOffsetArray(uint start, uint count, byte[] oids)
        {
            if (_ver == 2)
            {
                int sz = Repository.InternalConfig.IdBytes;
                byte[] data = new byte[4 * count];

                _fIdx!.Position = 8 /* header */ + 256 * 4 /* fanout */
                        + sz * _fanOut![255] // Hashes
                        + 4 * _fanOut[255] // Crc32
                        + 4 * start;

                if (data.Length != _fIdx.Read(data, 0, data.Length))
                    return Array.Empty<byte>();

                return data;
            }
            else if (_ver == 1)
            {
                // TODO: Data is interleaved
                return oids ?? throw new ArgumentNullException(nameof(oids));
            }
            else
                return Array.Empty<byte>();
        }

        private uint GetOffset(byte[] offsetArray, int index)
        {
            if (_ver == 2)
            {
                return ToHost(BitConverter.ToUInt32(offsetArray, index * 4));
            }
            else if (_ver == 1)
            {
                // oidArray = offsetArray with chunks of [4-byte length, 20 or 32 byte oid]
                return ToHost(BitConverter.ToUInt32(offsetArray, index * (4 + Repository.InternalConfig.IdBytes)));
            }
            else
                return uint.MaxValue;
        }

        private GitObjectId GetOid(byte[] oidArray, int index)
        {
            if (_ver == 2)
            {
                int idBytes = Repository.InternalConfig.IdBytes;
                return new GitObjectId(Repository.InternalConfig.IdType, oidArray.Skip(index * idBytes).Take(Repository.InternalConfig.IdBytes).ToArray());
            }
            else if (_ver == 1)
            {
                // oidArray = offsetArray with chunks of [4-byte length, 20 or 32 byte oid]
                int blockBytes = 4 + Repository.InternalConfig.IdBytes;
                return new GitObjectId(Repository.InternalConfig.IdType, oidArray.Skip(index * blockBytes + 4).Take(Repository.InternalConfig.IdBytes).ToArray());
            }

            throw new GitRepositoryException("Unsupported pack version");
        }

        public async override ValueTask<TGitObject?> Get<TGitObject>(GitObjectId objectId)
            where TGitObject : class
        {
            Init();

            if (_fanOut is null)
                return null;

            byte byte0 = objectId.Hash[0];

            uint start = (byte0 == 0) ? 0 : _fanOut![byte0-1];
            uint count = _fanOut![byte0] - start;            

            byte[] oids = GetOidArray(start, count);

            if (TryFindOid(oids, objectId, out var index))
            {
                var r = GetOffsetArray(index + start, 1, oids);
                var offset = GetOffset(r, 0);

                _fb ??= FileBucket.OpenRead(_packFile, !Repository.InternalConfig.NoAsync);

                var rdr = await _fb.DuplicateAsync(true);
                await rdr.ReadSkipAsync(offset);

                GitPackFrameBucket pf = new GitPackFrameBucket(rdr.NoClose(), GitObjectIdType.Sha1, Repository.ObjectRepository.ResolveByOid);

                await pf.ReadRemainingBytesAsync();

                return GitObject.FromBucket(Repository, pf, typeof(TGitObject), objectId) as TGitObject;
            }
            return null;
        }


        public async override IAsyncEnumerable<TGitObject> GetAll<TGitObject>()
            where TGitObject : class
        {
            Init();

            if (_fanOut is null || _fanOut[255] == 0)
                yield break;

            uint count = _fanOut[255];

            byte[] oids = GetOidArray(0, count);
            byte[] offsets = GetOffsetArray(0, count, oids);

            _fb ??= FileBucket.OpenRead(_packFile, !Repository.InternalConfig.NoAsync);

            for (int i = 0; i < count; i++)
            {
                GitObjectId objectId = GetOid(oids, i);
                long offset = GetOffset(offsets, i);

                var rdr = await _fb.DuplicateAsync(true);
                await rdr.ReadSkipAsync(offset);

                GitPackFrameBucket pf = new GitPackFrameBucket(rdr, GitObjectIdType.Sha1, Repository.ObjectRepository.ResolveByOid);

                await pf.ReadRemainingBytesAsync();

                var r = GitObject.FromBucket(Repository, pf, typeof(TGitObject), objectId) as TGitObject;

                if (r != null)
                    yield return r;
            }
        }
    }
}