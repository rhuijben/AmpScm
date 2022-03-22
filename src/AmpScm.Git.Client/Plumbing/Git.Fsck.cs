using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Client.Plumbing
{
    public class GitFsckArgs : GitPlumbingArgs
    {
        public bool Full { get; set; }
        public override void Verify()
        {
            //throw new NotImplementedException();
        }
    }

    partial class GitPlumbing
    {
        [GitCommand("fsck")]
        public static async ValueTask<string> Fsck(this GitPlumbingClient c, GitFsckArgs a)
        {
            a.Verify();
            var args = new List<string>();

            if (a.Full)
                args.Add("--full");

            var (_, txt) = await c.Repository.RunPlumbingCommandOut("fsck", args.ToArray());
            return txt;
        }
    }
}
