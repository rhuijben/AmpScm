using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Git;
using AmpScm.Buckets.Specialized;
using AmpScm.Git.Sets;

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

        internal GitCommit(GitRepository repository, GitObjectBucket rdr, GitId id)
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

        public GitCommit? Parent => GetParent(0, false);

        public GitId? ParentId => GetParentId(0, false);

        public int ParentCount
        {
            get
            {
                Read();

                return (_parent as Object[])?.Length ?? (_parent is null ? 0 : 1);
            }
        }

        private GitCommit? GetParent(int index, bool viaIndex = true)
        {
            Read();
            var p = _parent;

            var pp = p as object[];
            if (index < 0 || index >= (pp?.Length ?? ((pp is null && viaIndex) ? 0 : 1)))
                throw new IndexOutOfRangeException();

            if (pp is not null)
                p = pp[index];

            GitId id;
            if (p is GitCommit c)
                return c;
            else if (p is GitId oid)
                id = oid;
            else if (p is string ps && GitId.TryParse(ps, out id))
                SetParent(index, id);
            else
                return null;

            return SetParent(index, Repository.ObjectRepository.Get<GitCommit>(id).Result);
        }
        private GitId? GetParentId(int index, bool viaIndex = true)
        {
            Read();
            var p = _parent;

            var pp = p as object[];
            if (index < 0 || index >= (pp?.Length ?? ((pp is null && viaIndex) ? 0 : 1)))
                throw new IndexOutOfRangeException();

            if (pp is not null)
                p = pp[index];

            if (p is GitId id)
                return id;
            else if (p is GitObject o)
                return o.Id;
            else if (p is string ps && GitId.TryParse(ps, out id))
                return SetParent(index, id);
            else
                return null;
        }

        private T? SetParent<T>(int index, T? value)
            where T : class
        {
            if (value is not null)
            {
                if (_parent is object[] pp)
                    pp[index] = value;
                else if (index == 0)
                    _parent = value;
            }
            return value;
        }

        public IReadOnlyList<GitId> ParentIds => new IdList(this);

        public IReadOnlyList<GitCommit?> Parents => new ParentList(this);

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
            if (_tree is GitObjectBucket b)
            {
                await b.ReadTypeAsync();

                if (b.Type != GitObjectType.Commit)
                    throw new InvalidOperationException();

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

                            if (_parent is null)
                                _parent = id;
                            else if (_parent is object[] o)
                                _parent = o.Concat(new[] { id }).ToArray();
                            else
                                _parent = new object[] { _parent, id };
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

        public GitRevisionSet Revisions => new GitRevisionSet(Repository).AddCommit(this);

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

        private sealed class IdList : IReadOnlyList<GitId>
        {
            GitCommit Commit {get;}

            public IdList(GitCommit commit)
            {
                Commit = commit;
                Commit.Read();
            }

            public GitId this[int index] => Commit.GetParentId(index)!;

            public int Count => (Commit._parent == null) ? 0 : (Commit._parent as Object[])?.Length ?? 1;

            public IEnumerator<GitId> GetEnumerator()
            {
                object[]? parents = Commit._parent as object[];

                if (parents != null)
                {
                    for (int i = 0; i < parents.Length; i++)
                    {
                        if (parents[i] is GitId id)
                            yield return id;
                        else if (parents[i] is GitObject ob)
                            yield return ob.Id;
                        else
                            yield return Commit.GetParentId(i)!;
                    }
                }
                else
                {
                    var v = Commit.ParentId;

                    if (v != null)
                        yield return v;
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        private sealed class ParentList : IReadOnlyList<GitCommit?>
        {
            GitCommit Commit { get; }

            public ParentList(GitCommit commit)
            {
                Commit = commit;
                Commit.Read();
            }

            public GitCommit? this[int index] => Commit.GetParent(index);

            public int Count => Commit.ParentCount;

            public IEnumerator<GitCommit> GetEnumerator()
            {
                object[]? parents = Commit._parent as object[];

                if (parents != null)
                {
                    for (int i = 0; i < parents.Length; i++)
                    {
                        if (parents[i] is GitCommit c)
                            yield return c;
                        else
                            yield return Commit.GetParent(i)!;
                    }
                }
                else
                {
                    var v = Commit.Parent;

                    if (v != null)
                        yield return v;
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

    }

}
