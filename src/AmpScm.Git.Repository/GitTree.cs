using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Git;
using AmpScm.Buckets.Specialized;
using AmpScm.Git.Implementation;
using AmpScm.Git.Sets;

namespace AmpScm.Git
{
    public class GitTree : GitObject, IEnumerable<GitTreeEntry>, IAsyncEnumerable<GitTreeEntry>
    {
        List<GitTreeEntry> _entries = new List<GitTreeEntry>();
        private GitBucket? _rdr;
        BucketEolState? _eolState;

        internal GitTree(GitRepository repository, GitObjectId id)
            : base(repository, id)
        {
        }

        internal GitTree(GitRepository repository, GitBucket rdr, GitObjectId id)
            : this(repository, id)
        {
            _rdr = rdr;
        }

        public override GitObjectType Type => GitObjectType.Tree;

        private async ValueTask ReadNext()
        {
            if (_rdr == null)
                return;

            int val;
            string name;
            var (bb, eol) = await _rdr.ReadUntilEolFullAsync(BucketEol.Zero, _eolState ??= new BucketEolState());

            if (bb.IsEof)
            {
                var r = _rdr;
                _rdr = null;
                await r.DisposeAsync();
                return;
            }

            if (eol == BucketEol.Zero)
            {
                string v = bb.ToUTF8String(eol);

                var p = v.Split(new[] { ' ' }, 2);

                val = int.Parse(p[0]);
                name = p[1];
            }
            else
                throw new GitRepositoryException("Truncated tree");

            bb = await _rdr.ReadFullAsync(GitObjectId.HashLength(Id.Type));

            _entries.Add(NewGitTreeEntry(name, val, new GitObjectId(Id.Type, bb.ToArray())));
        }

        private GitTreeEntry NewGitTreeEntry(string name, int val, GitObjectId id)
        {
            if (val == 40000)
                return new GitDirectoryTreeEntry(this, name, id);
            else
                return new GitFileTreeEntry(this, name, val, id);
        }

        public async IAsyncEnumerator<GitTreeEntry> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            if (_rdr is null)
            {
                foreach (GitTreeEntry entry in _entries)
                    yield return entry;
            }

            int n = 0;
            while (true)
            {
                for (; n < _entries.Count; n++)
                {
                    yield return _entries[n];
                }

                if (n == _entries.Count && _rdr == null)
                {
                    yield break;
                }

                await ReadNext();
            }
        }

        public IEnumerator<GitTreeEntry> GetEnumerator()
        {
            if (_rdr is null)
                return _entries.GetEnumerator();

            return this.AsNonAsyncEnumerable().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        GitTreeItemCollection? _allFiles;
        GitTreeItemCollection? _allItems;
        public GitTreeItemCollection AllFiles => _allFiles ??= new GitTreeItemCollection(this, true);

        public GitTreeItemCollection AllItems => _allItems ??= new GitTreeItemCollection(this, false);
    }
}
