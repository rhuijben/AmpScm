using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Git.References;
using AmpScm.Git.Sets;

namespace AmpScm.Git
{
    public class GitReference : IGitNamedObject
    {
        protected GitReferenceRepository Repository { get; }
        object? _object;
        object? _commit;
        Lazy<GitId?>? _resolver;
        string? _shortName;

        internal GitReference(GitReferenceRepository repository, string name, Lazy<GitId?> resolver)
        {
            Repository = repository ?? throw new ArgumentNullException(nameof(repository));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _resolver = resolver;
        }

        internal GitReference(GitReferenceRepository repository, string name, GitId? value)
        {
            Repository = repository ?? throw new ArgumentNullException(nameof(repository));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _object = value;
        }

        public string Name { get; }

        public string ShortName
        {
            get
            {
                if (_shortName == null)
                {
                    if (Name.StartsWith("refs/heads/"))
                        _shortName = Name.Substring(11);
                    else if (Name.StartsWith("refs/remotes/"))
                        _shortName = Name.Substring(13);
                    else if (Name.StartsWith("refs/tags/"))
                        _shortName = Name.Substring(10);
                    else if (Name.StartsWith("refs/"))
                        _shortName = Name.Substring(5);
                    else
                        _shortName = Name;
                }

                return _shortName;
            }
        }

        public virtual async ValueTask ReadAsync()
        {
            if (_object is null)
            {
                _object ??= _resolver?.Value;
                _object ??= await Repository.Repository.References.GetAsync(Name);
            }

            if (_object is GitId oid)
            {
                _object = await Repository.Repository.GetAsync<GitObject>(oid) ?? _object;
            }
        }

        public virtual GitObject? Object
        {
            get
            {
                if (_object is not GitObject)
                    ReadAsync().GetAwaiter().GetResult();

                return _object as GitObject;
            }
        }

        public GitId? ObjectId
        {
            get
            {
                if (_object == null)
                    ReadAsync().GetAwaiter().GetResult();

                if (_object is GitId oid)
                    return oid;
                else if (_object is GitObject ob)
                    return ob.Id;
                else
                    return null;
            }
        }


        public virtual GitCommit? Commit
        {
            get
            {
                if (_commit is GitCommit commit)
                    return commit;
                else if (_object is GitTag tag)
                {
                    if (tag.Object is GitCommit c2)
                    {
                        _commit = c2;
                        return c2;
                    }
                }
                else if (_object is GitCommit c3)
                {
                    _commit = c3;
                    return c3;
                }
                else if (_commit is GitId oid)
                {
                    c3 = Repository.Repository.GetAsync<GitCommit>(oid).Result!;
                    _commit = c3;
                    return c3;
                }
                else if (Object is GitObject ob)
                {
                    if (ob is GitCommit c4)
                    {
                        _commit = c4;
                        return c4;
                    }
                    else if (ob is GitTag tag2)
                    {
                        c4 = (tag2.Object as GitCommit)!;
                        _commit = c4;
                        return c4;
                    }
                }
                return null;
            }
        }

        public GitRevisionSet Revisions => new GitRevisionSet();

        static HashSet<char> InvalidChars = new HashSet<char>(Path.GetInvalidFileNameChars().Concat(new [] { '^', '@', '{', '}' }));

        public static bool ValidName(string name, bool allowSpecialSymbols)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));

            char last = '\0';

            for (int i = 0; i < name.Length; last = name[i++])
            {
                if (char.IsLetterOrDigit(name, i))
                    continue;
                switch (name[i])
                {
                    case '\\':
                        return false;
                    case '.' when (last == '/' || last == '\0'):
                        return false;
                    case '/' when (last == '\0' || last == '\0'):
                        return false;
                    case '/':
                    case '.':
                        continue;
                    default:
                        if (char.IsControl(name, i) || char.IsWhiteSpace(name, i) || InvalidChars.Contains(name[i]))
                            return false;
                        break;
                }
            }
            return (name.Length > 1) && (allowSpecialSymbols || !AllUpper(name));
        }

        internal GitReference SetPeeled(GitId? peeled)
        {
            _commit = peeled;
            return this;
        }

        private static bool AllUpper(string name)
        {
            for (int i = 0; i < name.Length; i++)
            {
                if (!char.IsUpper(name, i) && name[i] != '_')
                    return false;
            }
            return true;
        }
    }
}
