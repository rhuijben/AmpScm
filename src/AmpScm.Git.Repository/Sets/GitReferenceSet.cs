﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AmpScm.Git.Implementation;

namespace AmpScm.Git.Sets
{
    public class GitReferenceSet : GitNamedSet<GitReference>
    {
        internal GitReferenceSet(GitRepository repository, Expression<Func<GitNamedSet<GitReference>>> rootExpression) 
            : base(repository, rootExpression)
        {
        }
    }
}
