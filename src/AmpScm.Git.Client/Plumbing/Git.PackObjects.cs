﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Client.Plumbing
{
    public class GitPackObjectsArgs : GitPlumbingArgs
    {
        public override void Verify()
        {
            throw new NotImplementedException();
        }
    }

    partial class GitPlumbing
    {
        [GitCommand("pack-objects")]
        public static async ValueTask PackObjects(this GitPlumbingClient c, GitPackObjectsArgs a)
        {
            a.Verify();
            //var (_, txt) = await c.Repository.RunPlumbingCommandOut("help", new[] { "-i", a.Command! ?? a.Guide! });

            await c.ThrowNotImplemented();
        }
    }
}
