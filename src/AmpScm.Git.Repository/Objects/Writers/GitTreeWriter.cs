using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Git.Objects;

namespace AmpScm.Git.Objects
{
    public class GitTreeWriter : GitObjectWriter, IGitPromisor<GitTree>, IEnumerable<KeyValuePair<string, GitObjectWriter>>
    {
        readonly SortedList<string, Item> _items = new SortedList<string, Item>(StringComparer.Ordinal);

        public GitTree? GitObject => throw new NotImplementedException();

        public override GitObjectType Type => GitObjectType.Tree;

        static bool IsValidName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;
            else if (name == ".")
                return false;
            else if (name.Contains('/', StringComparison.Ordinal))
                return false;

            return true;
        }

        public void Add<TGitObject>(string name, TGitObject item)
            where TGitObject : GitObject
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));
            else if (item is null)
                throw new ArgumentNullException(nameof(item));

            if (IsValidName(name))
            {
                if (_items.ContainsKey(name))
                    throw new ArgumentOutOfRangeException(nameof(name));

                _items.Add(name, new Item<TGitObject>(name, item));
            }
            else if (name.Contains('/', StringComparison.Ordinal))
            {
                var p = name.Split('/');
                GitTreeWriter tw = this;

                foreach (var si in p.Take(p.Length - 1))
                {
                    if (tw._items.TryGetValue(si, out var v)
                        && v.Promisor is GitTreeWriter subTw)
                    {
                        tw = subTw;
                    }
                    else
                    {
                        tw.Add(si, subTw = GitTreeWriter.CreateEmpty());
                        tw = subTw;
                    }
                }

                tw.Add(p.Last(), item);
            }
            else
                throw new ArgumentOutOfRangeException(nameof(name), name, "Invalid name");

            Id = null;
        }

        internal void PutId(GitId id)
        {
            Id ??= id;
        }

        public void Add<TGitObject>(string name, IGitPromisor<TGitObject> item)
            where TGitObject : GitObject
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));
            else if (item is null)
                throw new ArgumentNullException(nameof(item));

            if (IsValidName(name))
            {
                if (_items.ContainsKey(name))
                    throw new ArgumentOutOfRangeException(nameof(name));

                _items.Add(name, new Item<TGitObject>(name, item));
            }
            else if (name.Contains('/', StringComparison.Ordinal))
            {
                var p = name.Split('/');
                GitTreeWriter tw = this;

                foreach (var si in p.Take(p.Length - 1))
                {
                    if (tw._items.TryGetValue(si, out var v)
                        && v.Promisor is GitTreeWriter subTw)
                    {
                        tw = subTw;
                    }
                    else
                    {
                        tw.Add(si, subTw = GitTreeWriter.CreateEmpty());
                        tw = subTw;
                    }
                }

                tw.Add(p.Last(), item);
            }
            else
                throw new ArgumentOutOfRangeException(nameof(name), name, "Invalid name");

            Id = null;
        }

        public void Replace<TGitObject>(string name, TGitObject item)
            where TGitObject : GitObject
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));
            else if (item is null)
                throw new ArgumentNullException(nameof(item));

            if (IsValidName(name))
            {
                if (_items.ContainsKey(name))
                    throw new ArgumentOutOfRangeException(nameof(name));

                _items[name] = new Item<TGitObject>(name, item);
            }
            else if (name.Contains('/', StringComparison.Ordinal))
            {
                var p = name.Split('/');
                GitTreeWriter tw = this;

                foreach (var si in p.Take(p.Length - 1))
                {
                    if (tw._items.TryGetValue(si, out var v)
                        && v.Promisor is GitTreeWriter subTw)
                    {
                        tw = subTw;
                    }
                    else
                    {
                        tw.Add(si, subTw = GitTreeWriter.CreateEmpty());
                        tw = subTw;
                    }
                }

                tw.Replace(p.Last(), item);
            }
            else
                throw new ArgumentOutOfRangeException(nameof(name), name, "Invalid name");

            Id = null;
        }

        public void Replace<TGitObject>(string name, IGitPromisor<TGitObject> item)
            where TGitObject : GitObject
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));
            else if (item is null)
                throw new ArgumentNullException(nameof(item));

            if (IsValidName(name))
            {
                if (_items.ContainsKey(name))
                    throw new ArgumentOutOfRangeException(nameof(name));

                _items[name] = new Item<TGitObject>(name, item);
            }
            else if (name.Contains('/', StringComparison.Ordinal))
            {
                var p = name.Split('/');
                GitTreeWriter tw = this;

                foreach (var si in p.Take(p.Length - 1))
                {
                    if (tw._items.TryGetValue(si, out var v)
                        && v.Promisor is GitTreeWriter subTw)
                    {
                        tw = subTw;
                    }
                    else
                    {
                        tw.Add(si, subTw = GitTreeWriter.CreateEmpty());
                        tw = subTw;
                    }
                }

                tw.Replace(p.Last(), item);
            }
            else
                throw new ArgumentOutOfRangeException(nameof(name), name, "Invalid name");

            Id = null;
        }

        public override async ValueTask<GitId> WriteAsync(GitRepository repository)
        {
            if (Id is null || await repository.Trees.GetAsync(Id).ConfigureAwait(false) is null)
            {
                foreach (var i in _items.Values)
                {
                    await i.EnsureAsync(repository).ConfigureAwait(false);
                }

#pragma warning disable CA2000 // Dispose objects before losing scope
                Bucket b = new AggregateBucket(_items.Values.Select(x =>
                    new GitTreeElementRecord()
                    {
                        Name = x.Name,
                        Type = x.Type,
                        Id = x.Id ?? throw new InvalidOperationException("Id not set on entry")
                    }.AsBucket()).ToArray());
#pragma warning restore CA2000 // Dispose objects before losing scope

                Id = await WriteBucketAsObject(b, repository).ConfigureAwait(false);
            }

            return Id;
        }

        public async ValueTask<GitTree> WriteAndFetchAsync(GitRepository repository)
        {
            var id = await WriteAsync(repository).ConfigureAwait(false);
            return await repository.GetAsync<GitTree>(id).ConfigureAwait(false) ?? throw new InvalidOperationException();
        }

        abstract class Item
        {
            public string Name { get; set; }
            public GitTreeElementType Type { get; internal set; }

            public GitId? Id { get; set; }

            public abstract ValueTask EnsureAsync(GitRepository repository);

            public abstract object Promisor { get; }
        }

        class Item<TGitObject> : Item
            where TGitObject : GitObject
        {
            private TGitObject item;
            private IGitPromisor<TGitObject> _promisor;

            public Item(string name, TGitObject item)
            {
                Name = name;
                this.item = item;
                Id = item.Id ?? throw new InvalidOperationException("Id is null");
                Type = item.Type switch
                {
                    GitObjectType.Blob => GitTreeElementType.File,
                    GitObjectType.Tree => GitTreeElementType.Directory,
                    _ => GitTreeElementType.None
                };
            }

            public Item(string name, IGitPromisor<TGitObject> item1)
            {
                Name = name;
                this._promisor = item1;
                //Id = item1.Id ?? throw new InvalidOperationException($"No id on {Name}");
                Type = item1.Type switch
                {
                    GitObjectType.Blob => GitTreeElementType.File,
                    GitObjectType.Tree => GitTreeElementType.Directory,
                    _ => GitTreeElementType.None
                };
            }

            public override object Promisor => _promisor;

            public override async ValueTask EnsureAsync(GitRepository repository)
            {
                if (_promisor is not null)
                {
                    Id ??= _promisor.Id;

                    if (_promisor.Id is null || (await repository.Objects.GetAsync(_promisor.Id).ConfigureAwait(false)) is null)
                    {
                        Id = await _promisor.EnsureId(repository).ConfigureAwait(false);
                    }
                }
                else
                {
                    Id ??= item.Id;

                    if (Id is null || (await repository.Objects.GetAsync(Id).ConfigureAwait(false)) is null)
                    {
                        throw new NotImplementedException();
                    }
                }
            }
        }

        public static GitTreeWriter CreateEmpty()
        {
            return new GitTreeWriter();
        }

        public IEnumerator<KeyValuePair<string, GitObjectWriter>> GetEnumerator()
        {
            foreach (var i in _items)
            {
                yield return new KeyValuePair<string, GitObjectWriter>(i.Key, (GitObjectWriter)i.Value.Promisor);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
