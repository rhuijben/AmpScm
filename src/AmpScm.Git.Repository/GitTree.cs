using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Git;
using AmpScm.Buckets.Git.Objects;
using AmpScm.Buckets.Specialized;
using AmpScm.Git.Implementation;
using AmpScm.Git.Sets;

namespace AmpScm.Git
{
    public class GitTree : GitObject, IEnumerable<GitTreeEntry>, IAsyncEnumerable<GitTreeEntry>
    {
        List<GitTreeEntry> _entries = new List<GitTreeEntry>();
        private GitBucket? _rdr;

        internal GitTree(GitRepository repository, GitId id)
            : base(repository, id)
        {
        }

        internal GitTree(GitRepository repository, GitBucket rdr, GitId id)
            : this(repository, id)
        {
            _rdr = rdr;
        }

        public override GitObjectType Type => GitObjectType.Tree;

        private async ValueTask ReadNext()
        {
            if (_rdr == null)
                return;

            if (!(_rdr is GitTreeReadBucket rdr))
            {
                if (_rdr is Bucket b)
                    _rdr = rdr = new GitTreeReadBucket(b, Repository.InternalConfig.IdType);
                else
                    return;
            }

            var el = await rdr.ReadTreeElement();

            if (el.IsEof)
            {
                _rdr = null;
                await rdr.DisposeAsync();
                return;
            }

            _entries.Add(NewGitTreeEntry(el.Value));
        }

        private GitTreeEntry NewGitTreeEntry(GitTreeElement value)
        {
            if (value.Type == GitTreeElementType.Directory)
                return new GitDirectoryTreeEntry(this, value.Name, value.Id);
            else
                return new GitFileTreeEntry(this, value.Name, value.Type, value.Id);
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
