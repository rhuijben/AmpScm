using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Git;
using AmpScm.Git.Repository;

namespace AmpScm.Git
{
    partial class GitRepository
    {
        internal class GitInternalConfigAccess
        {
            public GitObjectIdType IdType { get; } = GitObjectIdType.Sha1;

            public bool NoAsync => false;

            internal GitInternalConfigAccess(GitObjectIdType type)
            {
                IdType = type;
            }
                
        }
        internal GitInternalConfigAccess InternalConfig { get; private set;  } = new GitInternalConfigAccess(GitObjectIdType.Sha1);


        GitConfiguration LoadConfig()
        {
            return new GitConfiguration(this, GitDir);
        }

        internal void SetSHA256() // Called from repository object store on config verify
        {
            InternalConfig = new GitInternalConfigAccess(GitObjectIdType.Sha256);
        }
    }
}
