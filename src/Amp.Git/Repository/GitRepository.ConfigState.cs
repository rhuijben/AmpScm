using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amp.Buckets.Git;

namespace Amp.Git
{
    partial class GitRepository
    {
        internal class GitInternalConfigAccess
        {
            public GitObjectIdType IdType => GitObjectIdType.Sha1;
            public int IdBytes => 20;

            public bool NoAsync => false;
        }
        internal GitInternalConfigAccess InternalConfig { get; } = new GitInternalConfigAccess();

    }
}
