﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Amp.Git.Client.Plumbing
{
    public class GitVariableArgs : GitPlumbingArgs
    {
        public override void Verify()
        {
            throw new NotImplementedException();
        }
    }

    partial class GitPlumbing
    {
        [GitCommand("var")]
        public static async ValueTask Variable(this GitPlumbingClient c, GitVariableArgs a)
        {
            a.Verify();
            //var (_, txt) = await c.Repository.RunPlumbingCommandOut("help", new[] { "-i", a.Command! ?? a.Guide! });

            await c.ThrowNotImplemented();
        }
    }
}
