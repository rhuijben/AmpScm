using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Git;

namespace AmpScm.Git
{
    [DebuggerDisplay("{DebuggerDisplay, nq}")]
    public class GitCommit : GitObject
    {
        object? _tree;
        object? _parent;
        Dictionary<string, string>? _headers;
        string? _message;
        string? _summary;
        GitSignature? _author;
        GitSignature? _committer;

        public GitCommit(GitRepository repository, GitObjectId id)
            : base(repository, id)
        {
            _tree = null;
        }

        public GitCommit(GitRepository repository, GitBucket rdr, GitObjectId id)
            : this(repository, id)
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

                if (_tree is string s && !string.IsNullOrEmpty(s) && GitObjectId.TryParse(s, out var oid))
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

                if (_parent is string s && GitObjectId.TryParse(s, out var oid))
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
            if (_tree is Bucket b)
            {
                _tree = "";
                using var s = b.AsReader();

                while (s.ReadLine() is string line)
                {
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

                        default:
                            _headers ??= new Dictionary<string, string>();
                            if (_headers.TryGetValue(parts[0], out var v))
                                _headers[parts[0]] = v + "\n" + parts[1];
                            else
                                _headers[parts[0]] = parts[1];
                            break;
                    }
                }

                _message = s.ReadToEnd();
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
