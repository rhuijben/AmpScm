using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        [GitCommand("help")]
        public static async ValueTask<string> Help(this GitPlumbingClient c, GitHelpArgs a)
        {
            a.Verify();
            var (_, txt) = await c.Repository.RunPlumbingCommandOut("help", new[] { "-i", a.Command! ?? a.Guide! });

            return txt ?? "";
        }

        public static async ValueTask<string[]> HelpUsage(this GitPlumbingClient c, string name)
        {
            if (!typeof(GitPlumbing).GetMethods().Any(x => x.GetCustomAttribute<GitCommandAttribute>()?.Name == name))
                throw new ArgumentOutOfRangeException();

            List<string> results = new List<string>();
            await foreach (var line in c.Repository.WalkPlumbingCommand(name, new[] { "-h" }, expectedResults: new[] { 129 }))
            {
                results.Add(line);
            }

            return results.ToArray();
        }
    }
}
