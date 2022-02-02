using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Git;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Git
{
    [DebuggerDisplay("{DebuggerDisplay, nq}")]
    public sealed class GitCommit : GitObject
    {
        object? _tree;
        object? _parent;
        Dictionary<string, string>? _headers;
        string? _encoding;
        string? _message;
        string? _summary;
        GitSignature? _author;
        GitSignature? _committer;

        internal GitCommit(GitRepository repository, GitBucket rdr, GitId id)
            : base(repository, id)
        {
            _tree = rdr;
        }

        public override sealed GitObjectType Type => GitObjectType.Commit;

        public GitTree Tree
        {
            get
            {
                if (_tree is GitTree tree)
                    return tree;

                Read();

                if (_tree is string s && !string.IsNullOrEmpty(s) && GitId.TryParse(s, out var oid))
                {
                    _tree = oid;

                    try
                    {
                        var t = Repository.ObjectRepository.Get<GitTree>(oid).Result; // BAD async

                        if (t != null)
                        {
                            _tree = t;
                            return t;
                        }
                    }
                    catch
                    {
                        _tree = s; // Continue later
                        throw;
                    }
                }

                return null!;
            }
        }

        public GitCommit? Parent
        {
            get
            {
                if (_parent is GitCommit parent)
                    return parent;

                Read();

                if (_parent is string s && GitId.TryParse(s, out var oid))
                {
                    _parent = oid;

                    var t = Repository.ObjectRepository.Get<GitCommit>(oid).Result; // BAD async

                    if (t != null)
                    {
                        _parent = t;
                        return t;
                    }
                }

                return null;
            }
        }

        public string? Message
        {
            get
            {
                if (_message is null)
                    Read();

                return _message;
            }
        }

        public string? Summary
        {
            get
            {
                return _summary ?? (_summary = GitTools.CreateSummary(Message));
            }
        }

        public GitSignature Author
        {
            get
            {
                if (_author is null)
                    Read();

                return _author!;
            }
        }

        public GitSignature? Committer
        {
            get
            {
                if (_committer is null)
                    Read();

                return _committer;
            }
        }

        private void Read()
        {
            ReadAsync().GetAwaiter().GetResult();
        }

        public override async ValueTask ReadAsync()
        {
            if (_tree is Bucket b)
            {
                _tree = "";
                BucketEolState? _eolState = null;

                while (true)
                {
                    var (bb, eol) = await b.ReadUntilEolFullAsync(BucketEol.LF, _eolState ??= new BucketEolState());

                    if (bb.IsEof || bb.Length == eol.CharCount())
                        break;

                    string line = bb.ToUTF8String(eol);

                    if (line.Length == 0)
                        break;

                    var parts = line.Split(new[] { ' ' }, 2);
                    switch (parts[0])
                    {
                        case "tree":
                            _tree = parts[1];
                            break;
                        case "parent":
                            var id = parts[1];

                            if (_parent is string pp)
                                id = pp + " " + id;

                            _parent = id;
                            break;
                        case "author":
                            _author = new GitSignature(parts[1]);
                            break;
                        case "committer":
                            _committer = new GitSignature(parts[1]);
                            break;
                        case "encoding":
                            _encoding = parts[1];
                            break;
                        case "mergetag":
                            break;

                        case "gpgsig":
                            break; // Ignored for now

                        default:
                            if (!char.IsWhiteSpace(line, 0))
                            {
                                _headers ??= new Dictionary<string, string>();
                                if (_headers.TryGetValue(parts[0], out var v))
                                    _headers[parts[0]] = v + "\n" + parts[1];
                                else
                                    _headers[parts[0]] = parts[1];
                            }
                            break;
                    }
                }

                while (true)
                {
                    var (bb, _) = await b.ReadUntilEolFullAsync(BucketEol.Zero, _eolState ??= new BucketEolState());

                    if (bb.IsEof)
                        break;

                    _message += bb.ToUTF8String();
                }
            }
        }

        [DebuggerHidden]
        private string DebuggerDisplay
        {
            get
            {
                if (_message == null)
                    return $"Commit {Id:x12}";
                else
                    return $"Commit {Id:x12} - {Summary}";
            }
        }
    }

}
