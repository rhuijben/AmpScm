﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Client
{
    public class GitClient : GitRepository
    {
        public GitClient(string path)
            : base(InternalSetupArgs(path))
        {
        
        }
    }
}
