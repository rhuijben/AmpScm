using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Specialized;
using AmpScm.Git;

namespace AmpScm.Buckets.Git
{
    public record GitReferenceLogRecord
    {
        public GitId Original { get; init; } = default!;
        public GitId Target { get; init; } = default!;
        public GitSignatureRecord Signature { get; init; } = default!;
        public string? Summary { get; init; } = default!;
    }

    public record GitSignatureRecord
    {
        public string Name { get; init; } = default!;
        public string Email { get; init; } = default!;
        public DateTimeOffset When { get; init; }

        public override string ToString()
        {
            string offsetMinutes;

            if (When.Offset == TimeSpan.Zero)
                offsetMinutes = "+0000";
            else
            {
                int mins = (int)When.Offset.TotalMinutes;

                int hours = mins / 60;

                offsetMinutes = (mins + (hours * 100) - (hours * 60)).ToString("+0000");


            }

            return $"{Name} <{Email}> {When.ToUnixTimeSeconds()} {offsetMinutes}";
        }
    }

    public class GitReferenceLogBucket : GitBucket
    {
        GitId? _lastId;
        int? _idLength;

        public GitReferenceLogBucket(Bucket inner) : base(inner)
        {
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            while (await (ReadGitReferenceLogRecordAsync().ConfigureAwait(false)) != null)
            {

            }
            return BucketBytes.Eof;
        }

        public async ValueTask<GitReferenceLogRecord?> ReadGitReferenceLogRecordAsync()
        {
            var (bb, eol) = await Inner.ReadUntilEolFullAsync(BucketEol.LF, null).ConfigureAwait(false);

            if (bb.IsEof)
                return null;

            int prefix = bb.IndexOf((byte)'\t', (2 * _idLength + 2) ?? 0);

            if (!_idLength.HasValue)
            {
                _idLength = bb.IndexOf((byte)' ');

                if (prefix < 0 || _idLength < GitId.HashLength(GitIdType.Sha1) * 2 || _idLength * 2 + 2 > prefix)
                    throw new GitBucketException($"Unable to determine reference log format in {Inner.Name} bucket");
            }

            return new GitReferenceLogRecord
            {
                Original = ReadGitId(bb, 0) ?? throw new GitBucketException($"Bad {nameof(GitReferenceLogRecord.Original)} OID in RefLog line from {Inner.Name}"),
                Target = ReadGitId(bb, _idLength.Value + 1) ?? throw new GitBucketException($"Bad {nameof(GitReferenceLogRecord.Target)} OID in RefLog line from {Inner.Name}"),
                Signature = ReadSignature(bb.Slice(0, prefix).Slice(2 * (_idLength.Value + 1))),
                Summary = bb.Slice(prefix + 1).ToUTF8String(eol)
            };
        }

        private GitId? ReadGitId(BucketBytes bb, int offset)
        {
            var s = bb.ToASCIIString(offset, _idLength!.Value);

            if (GitId.TryParse(s, out var oid))
            {
                oid = (_lastId == oid) ? _lastId : oid;
                _lastId = oid;
                return oid;
            }
            else
                return null;
        }

        private static GitSignatureRecord ReadSignature(BucketBytes bb)
        {
            int n = bb.IndexOf((byte)'<');
            int n2 = bb.IndexOf((byte)'>', n);
            return new GitSignatureRecord
            {
                Name = bb.Slice(0, n).ToUTF8String().TrimEnd(),
                Email = bb.Slice(n + 1, n2 - n).ToUTF8String(),
                When = ParseWhen(bb.Slice(n2 + 2).ToUTF8String())
            };
        }

        private static DateTimeOffset ParseWhen(string value)
        {
            string[] time = value.Split(new[] { ' ' }, 2);
            if (int.TryParse(time[0], out var unixtime) && int.TryParse(time[1], out var offset))
            {
                return DateTimeOffset.FromUnixTimeSeconds(unixtime).ToOffset(TimeSpan.FromMinutes((offset / 100) * 60 + (offset % 100)));
            }
            return DateTimeOffset.Now;
        }
    }
}
