using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Objects
{
    public class GitObjectFactory
    {
        private GitObjectFactory()
        { }



        public static GitObjectFactory Instance { get; } = new GitObjectFactory();
    }
}
