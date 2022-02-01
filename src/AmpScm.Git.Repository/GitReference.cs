using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Git.Sets;

namespace AmpScm.Git
{
    public class GitReference : IGitNamedObject
    {
        public string Name => throw new NotImplementedException();

        public ValueTask ReadAsync()
        {
            throw new NotImplementedException();
        }

        public GitObject Object => null;

        public GitCommit Commit => (Object as GitCommit) ?? ((Object as GitTag)?.Object as GitCommit);
    }
}
