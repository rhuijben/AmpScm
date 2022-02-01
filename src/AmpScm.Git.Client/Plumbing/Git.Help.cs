using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Client.Plumbing
{
    public class GitHelpArgs : GitPlumbingArgs
    {
        public string? Command { get; set; }
        public string? Guide { get; set; }

        public override void Verify()
        {
            if (!(string.IsNullOrEmpty(Command) ^ string.IsNullOrEmpty(Guide)))
                throw new ArgumentOutOfRangeException($"{nameof(Command)} or {nameof(Guide)} should be set");
        }
    }

    partial class GitPlumbing
    {
        [GitCommand("git-help")]
        public static async ValueTask<string> GitHelp(this GitPlumbingClient c, GitHelpArgs a)
        {
            a.Verify();
            var (_, txt) = await c.Repository.RunPlumbingCommandOut("help", new[] { "-i", a.Command! ?? a.Guide! });

            return txt ?? "";
        }
    }
}
