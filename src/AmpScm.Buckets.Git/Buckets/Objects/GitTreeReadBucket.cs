using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Git;
using AmpScm.Buckets.Specialized;
using AmpScm.Git;

namespace AmpScm.Buckets.Git.Objects
{
    /// <summary>
    /// 
    /// </summary>
    public enum GitTreeElementType
    {
        None = 0,
        /// <summary>
        /// 040000 - Directory (Id->Tree)
        /// </summary>
        Directory = 0x4000,

        /// <summary>
        /// 0100644 - File (Id->Blob)
        /// </summary>
        File = 0x81A4,
        /// <summary>
        /// 0100644 - Executable (Id->Blob)
        /// </summary>
        FileExcutable = 0x81ED,

        /// <summary>
        /// 0120000 - Symlink (Id->Blob)
        /// </summary>
        SymbolicLink = 0xA000,

        /// <summary>
        /// 0160000 - GitLink/SubModule (Id->Commit)
        /// </summary>
        GitCommitLink = 0xE000,
    }

    public record GitTreeElementRecord
    {
        public GitTreeElementType Type { get; internal init; }
        public string Name { get; internal init; } = default!;
        public GitId Id { get; internal init; } = default!;
    }

    public class GitTreeReadBucket : GitBucket
    {
        readonly GitIdType _idType;
        bool _checkedType;

        public GitTreeReadBucket(Bucket inner, GitIdType idType) : base(inner)
        {
            _idType = idType;
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            while (await ReadTreeElementRecord() != null)
            {

            }

            return BucketBytes.Eof;
        }

        public async ValueTask<GitTreeElementRecord> ReadTreeElementRecord()
        {
            if (!_checkedType && Inner is GitObjectBucket gobb)
            {
                await gobb.ReadTypeAsync();

                if (gobb.Type != GitObjectType.Tree)
                    throw new GitBucketException($"Bucket {gobb.Name} is not Tree but {gobb.Type}");

                _checkedType = true;
            }

            var (bb, eol) = await Inner.ReadUntilEolFullAsync(BucketEol.Zero, null);

            if (bb.IsEof)
                return null;

            if (eol != BucketEol.Zero)
                throw new GitBucketException("Truncated tree");

            int nSep = bb.IndexOf((byte)' ');
            if (nSep < 0)
                throw new GitBucketException("Truncated tree. No mask separator");

            string name = bb.ToUTF8String(nSep + 1, bb.Length - nSep - 1, eol);
            string mask = bb.ToASCIIString(0, nSep);

            bb = await Inner.ReadFullAsync(GitId.HashLength(_idType));

            if (nSep < 0)
                throw new GitBucketException("Truncated tree. Incomplete hash");

            var id = new GitId(_idType, bb.ToArray());

            var val = Convert.ToInt32(mask, 8);

            return new GitTreeElementRecord { Name = name, Type = (GitTreeElementType)val, Id = id };
        }
    }
}
