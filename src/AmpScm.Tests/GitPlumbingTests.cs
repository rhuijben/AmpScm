using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Git;
using AmpScm.Git.Client.Plumbing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AmpScm.Tests
{
    [TestClass]
    public class GitPlumbingTests
    {
        public TestContext TestContext { get; set; } = null!;


        static string[] ignored = new[] { "for-each-repo" }; // Experimental, and 100% unneeded for our usecase

        [TestMethod]
        public async Task GitHelpToPlumbing()
        {
            using (var repo = GitRepository.Init(Path.Combine(TestContext.TestRunResultsDirectory, "GitHelp")))
            {
                string commandList = await repo.GetPlumbing().GitHelp(new GitHelpArgs { Command = "-a" });

                var implementedCommands = new HashSet<string>(
                    typeof(GitPlumbing).GetMethods()
                        .Where(x => x.IsPublic && x.IsStatic)
                        .Select(x => x.GetCustomAttribute<GitCommandAttribute>()?.Name ?? "")
                        .Where(x => x.Length > 0).Distinct());

                string? group = null;
                foreach (string command in commandList.Split('\n'))
                {
                    if (string.IsNullOrWhiteSpace(command))
                        group = null;
                    else if (char.IsLetterOrDigit(command, 1))
                        group = command;
                    else if (group != null)
                    {
                        string cmd = command.Trim().Split(' ')[0];
                        if ((group.StartsWith("Low-level") && !group.Contains("Internal") && !group.Contains("Syncing Repositories"))
                            || implementedCommands.Contains(cmd))
                        {


                            if (ignored.Contains(cmd))
                                continue;

                            string[] parts = cmd.Split('-');

                            if (parts[0].StartsWith("mk"))
                                parts = new string[] { "make", parts[0].Substring(2) }.Concat(parts.Skip(1)).ToArray();

                            for (int i = 0; i < parts.Length; i++)
                            {
                                if (parts[i] == "ref")
                                    parts[i] = "reference";
                                if (parts[i] == "refs")
                                    parts[i] = "references";
                                else if (parts[i] == "ls")
                                    parts[i] = "list";
                                else if (parts[i] == "repo")
                                    parts[i] = "repository";
                                else if (parts[i] == "rev")
                                    parts[i] = "revision";
                                else if (parts[i] == "var")
                                    parts[i] = "variable";
                            }

                            string name = string.Join("", parts.Select(x => x.Substring(0, 1).ToUpperInvariant() + x.Substring(1)));

                            if (!typeof(GitPlumbing).GetMethods().Any(x => x.Name == name))
                            {
                                Assert.Fail($"Method {name} is missing on {nameof(GitPlumbing)}");
                            }
                            else if (typeof(GitPlumbing).Assembly.GetType(typeof(GitPlumbing).Namespace + $".Git{name}Args") == null)
                            {
                                Assert.Fail($"Class Amp.Git.Client.Plumbing.Git{name}Args is missing");
                            }

                            var m = typeof(GitPlumbing).GetMethods().FirstOrDefault(x => x.Name == name && x.GetParameters().Length == 2);

                            if (m != null)
                            {
                                if (m.GetCustomAttributes<GitCommandAttribute>().FirstOrDefault() is GitCommandAttribute a)
                                {
                                    Assert.AreEqual(cmd, a.Name, $"Gitcommand properly documented on {m.DeclaringType}.{m.Name}()");
                                }
                                else
                                    Assert.Fail($"GitCommandAttribute not set on {m.DeclaringType}.{m.Name}()");

                                Assert.AreEqual($"Git{name}Args", m.GetParameters()[1].ParameterType.Name, "Parameter on {m.DeclaringType}.{m.Name}() as expected");
                            }
                        }
                    }
                }
            }
        }
    }
}
