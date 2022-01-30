using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Amp.Git.Client.Plumbing
{
    public abstract class GitPlumbingArgs
    {
        public abstract void Verify();
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class GitCommandAttribute : Attribute
    {
        public GitCommandAttribute(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }

    public static partial class GitPlumbing
    {
        public static GitPlumbingClient GetPlumbing(this GitRepository repository)
        {
            return new GitPlumbingClient(repository);
        }
    }
}
