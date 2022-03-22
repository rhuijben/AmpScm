using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Objects
{
    public interface IGitPromisor<TGitObject>
        where TGitObject : GitObject
    {
        GitId? Id { get; }

        GitObjectType Type { get; }

        ValueTask<GitId> WriteAsync(GitRepository repository);

        ValueTask<TGitObject> WriteAndFetchAsync(GitRepository repository);


        ValueTask<GitId> EnsureId(GitRepository repository);
    }
}
