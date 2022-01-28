using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amp.Git.Implementation;

namespace Amp.Git.Sets
{
    public class GitTreeItemCollection : IEnumerable<GitTreeItem>, IAsyncEnumerable<GitTreeItem>, IReadOnlyCollection<GitTreeItem>
    {
        readonly GitTree gitTree;
        readonly bool justFiles;
        int _count;

        internal GitTreeItemCollection(GitTree gitTree, bool justFiles)
        {
            this.gitTree = gitTree;
            this.justFiles = justFiles;
        }

        public int Count => throw new NotImplementedException();

        public async IAsyncEnumerator<GitTreeItem> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            Stack<(IAsyncEnumerator<GitTreeEntry>, string)> inside = null;

            IAsyncEnumerator<GitTreeEntry> cur = gitTree.GetAsyncEnumerator();
            string path = "";

            do
            {
                while (await cur.MoveNextAsync())
                {
                    var c = cur.Current;

                    if (c is GitDirectoryTreeEntry dir)
                    {
                        if (!justFiles)
                            yield return new GitTreeItem(path + c.Name, c);

                        inside ??= new Stack<(IAsyncEnumerator<GitTreeEntry>, string)>();

                        await dir.Read();

                        var t = dir.Tree?.GetAsyncEnumerator();

                        if (t != null)
                        {
                            inside.Push((cur, path));

                            path += dir.EntryName;
                            cur = t;
                        }
                    }
                    else
                    {
                        yield return new GitTreeItem(path + c.Name, c);
                    }
                }

                await cur.DisposeAsync();

                if (inside?.Count > 0)
                {
                    (cur, path) = inside.Pop();
                }
                else
                    break;
            }
            while (cur != null);
        }

        public IEnumerator<GitTreeItem> GetEnumerator()
        {
            return this.AsNonAsyncEnumerable().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    public struct GitTreeItem
    {
        public GitTreeItem(string path, GitTreeEntry entry) : this()
        {
            Path = path;
            Entry = entry;
        }

        public string Name => Entry.Name;
        public string EntryName => Entry.EntryName;
        public string Path { get; }
        public GitTreeEntry Entry { get; }
    }
}
