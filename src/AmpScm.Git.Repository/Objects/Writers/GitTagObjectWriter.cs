using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Objects
{
    public class GitTagObjectWriter : GitObjectWriter<GitTagObject>
    {
        private GitTagObjectWriter()
        {

        }
        public sealed override GitObjectType Type => GitObjectType.Tag;


        public override ValueTask<GitId> WriteToAsync(GitRepository repository)
        {
            throw new NotImplementedException();
        }

        internal static GitTagObjectWriter Create(IGitLazy<GitObject> obj)
        {
            throw new NotImplementedException();
        }

        internal static GitTagObjectWriter Create(GitObject obj)
        {
            return Create((IGitLazy<GitObject>)obj);
        }
    }
}
