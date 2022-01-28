using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amp.Buckets;
using Amp.Buckets.Git;
using Amp.Buckets.Specialized;
using Amp.Git.Implementation;
using Amp.Git.Sets;

namespace Amp.Git
{
    public class GitTree : GitObject, IEnumerable<GitTreeEntry>, IAsyncEnumerable<GitTreeEntry>
    {
        List<GitTreeEntry> _entries = new List<GitTreeEntry>();
        private GitBucket? _rdr;

        public GitTree(GitRepository repository, GitObjectId id)
            : base(repository, id)
        {
        }

        public GitTree(GitRepository repository, GitBucket rdr, GitObjectId id)
            : this(repository, id)
        {
            _rdr = rdr;
        }

        private async ValueTask ReadNext()
        {
            if (_rdr == null)
                return;

            int val;
            string name;
            var bb = await _rdr.ReadUntilAsync((byte)'\0');

            if (bb.IsEof)
            {
                var r = _rdr;
                _rdr = null;
                await r.DisposeAsync();
                return;
            }

            if (!bb.IsEmpty && bb[bb.Length - 1] == '\0')
            {
                string v = Encoding.UTF8.GetString(bb.Slice(0, bb.Length - 1).ToArray());

                var p = v.Split(new[] { ' ' }, 2);

                val = int.Parse(p[0]);
                name = v.Split(new[] { ' ' }, 2)[1];
            }
            else
                throw new GitRepositoryException("Truncated tree");

            bb = await _rdr.ReadFullAsync(Repository.InternalConfig.IdBytes);

            _entries.Add(NewGitTreeEntry(name, val, new GitObjectId(Repository.InternalConfig.IdType, bb.ToArray())));
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

        GitTreeItemCollection _allFiles;
        GitTreeItemCollection _allItems;
        public GitTreeItemCollection AllFiles => _allFiles ??= new GitTreeItemCollection(this, true);

        public GitTreeItemCollection AllItems => _allItems ??= new GitTreeItemCollection(this, false);
    }
}
