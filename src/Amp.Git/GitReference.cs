using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amp.Git.Sets;

namespace Amp.Git
{
    public class GitReference : IGitNamedObject
    {
        public string Name => throw new NotImplementedException();

        public ValueTask ReadAsync()
        {
            throw new NotImplementedException();
        }
    }
}
