﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Objects
{
    public interface IGitPromisor<TGitObject> : IGitLazy<TGitObject>
        where TGitObject : GitObject
    {
        GitObjectType Type { get; }

        ValueTask<TGitObject> WriteAndFetchAsync(GitRepository repository);
    }
}
