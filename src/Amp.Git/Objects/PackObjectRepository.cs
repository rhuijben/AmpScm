using System;
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
        private string pack;
        FileStream? _fIdx;
        Bucket? _fb;
        int _ver;
        uint[]? _fanOut;

        public PackObjectRepository(GitRepository repository, string pack)
            : base(repository)
        {
            this.pack = pack;
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
            _fIdx ??= File.OpenRead(Path.ChangeExtension(pack, ".idx"));

            if (_ver == 0)
            {
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
                        fanOutOffset = -1;
                        _ver = -1;
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
                        for(int i = 0; i < 256; i++)
                        {
                            _fanOut[i] = ToHost(BitConverter.ToUInt32(fanOut, i * 4));
                        }
                    }
                }
            }
        }

        public async override ValueTask<TGitObject?> Get<TGitObject>(GitObjectId objectId)
            where TGitObject : class
        {
            Init();

            if (_ver == 2)
            {
                uint count = _fanOut![255];

                byte[] oids = GetOidArray(0, count);

                if (TryFindOid(oids, objectId, out var offset))
                {
                    var r = GetOffsetArray(0+ offset, 1);

                    _fb ??= FileBucket.OpenRead(pack);

                    var rdr = await _fb.DuplicateAsync(true);
                    await rdr.ReadSkipAsync(r[0]);

                    GitPackFrameBucket pf = new GitPackFrameBucket(rdr.SeekOnReset().NoClose(), GitObjectIdType.Sha1, Repository.ObjectRepository.ResolveByOid);

                    await pf.ReadRemainingBytesAsync();

                    return GitObject.FromBucket(Repository, pf, typeof(TGitObject), objectId) as TGitObject;
                }

                //GC.KeepAlive(oids);
            }
            return null;
        }

        private bool TryFindOid(byte[] oids, GitObjectId objectId, out int offset)
        {
            int first = 0, c = oids.Length / 20;

            while (first < c - 1)
            {
                int mid = (first + c) / 2;

                var check = new Span<byte>(oids, 20 * mid, 20);

                for (int i = 0; i < objectId.Hash.Length; i++)
                {
                    int n = objectId.Hash[i] - check[i];

                    if (n == 0)
                        continue;

                    if (n < 0)
                        c = mid;
                    else
                        first = mid + 1;
                    break;
                }
            }

            var check2 = new Span<byte>(oids, 20 * first, 20);
            for (int i = 0; i < objectId.Hash.Length; i++)
            {
                int n = objectId.Hash[i] - check2[i];

                if (n != 0)
                {
                    offset = 0;
                    return false;
                }
            }
            offset = first;
            return true;
        }

        private byte[] GetOidArray(int start, uint count)
        {
            if (_ver == 2)
            {
                int sz = 20;
                byte[] data = new byte[sz * count];

                _fIdx!.Position = 8 /* header */ + 256 * 4 /* fanout */ + sz * start;

                if (data.Length != _fIdx.Read(data, 0, data.Length))
                    return Array.Empty<byte>();

                return data;
            }
            else if (_ver == 1)
            {
                // TODO: Data is interleaved
                return Array.Empty<byte>();
            }
            else
                return Array.Empty<byte>();
        }

        private uint[] GetOffsetArray(int start, uint count)
        {
            if (_ver == 2)
            {
                int sz = 20;
                byte[] data = new byte[4 * count];

                _fIdx!.Position = 8 /* header */ + 256 * 4 /* fanout */
                        + sz * _fanOut![255] // Hashes
                        + 4 * _fanOut[255] // Crc32
                        + 4 * start;

                if (data.Length != _fIdx.Read(data, 0, data.Length))
                    return Array.Empty<uint>();

                uint[] result = new uint[count];
                for (int i = 0; i < count; i++)
                    result[i] = ToHost(BitConverter.ToUInt32(data, i*4));
                return result;
            }
            else if (_ver == 1)
            {
                // TODO: Data is interleaved
                return Array.Empty<uint>();
            }
            else
                return Array.Empty<uint>();
        }

        public async override IAsyncEnumerable<TGitObject> GetAll<TGitObject>()
        {
            Init();
            yield break;
        }
    }
}
