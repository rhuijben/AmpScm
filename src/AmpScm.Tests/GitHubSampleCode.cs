using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Git;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AmpScm.Tests
{

    [TestClass]
    public class GitHubSampleCode
    {
        [TestMethod]
        public async Task WelcomeSample()
        {
            using (var repo = await GitRepository.OpenAsync(Environment.CurrentDirectory))
            {
                await foreach (var r in repo.Head.Revisions)
                {
                    Console.WriteLine($"Commit: {r.Commit.Id}");
                    Console.WriteLine($"Author: {r.Commit.Author?.Name} <{r.Commit.Author?.Email}>");
                    Console.WriteLine("");
                    Console.WriteLine(r.Commit.Message?.TrimEnd() + "\n");
                }
            }
        }
    }
}
