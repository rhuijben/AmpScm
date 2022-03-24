using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Objects
{
    public class GitTagObjectWriter : GitObjectWriter, IGitPromisor<GitTagObject>
    {
        public sealed override GitObjectType Type => GitObjectType.Tag;

        public ValueTask<GitTagObject> WriteAndFetchAsync(GitRepository repository)
        {
            throw new NotImplementedException();
        }

        public override ValueTask<GitId> WriteToAsync(GitRepository toRepository)
        {
            throw new NotImplementedException();
        }

        internal void PutId(GitId id)
        {
            throw new NotImplementedException();
        }
    }
}
