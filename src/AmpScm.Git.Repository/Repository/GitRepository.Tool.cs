using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Git.Implementation;
using AmpScm.Git.Repository;

namespace AmpScm.Git
{
    partial class GitRepository
    {
        internal protected ValueTask<int> RunPlumbingCommand(string command, params string[] args)
        {
            return RunPlumbingCommand(command, args, stdinText: null, expectedResults: null);
        }

        internal protected async ValueTask<int> RunPlumbingCommand(string command, string[] args, string? stdinText = null, int[]? expectedResults = null)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo(GitConfiguration.GitProgramPath)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = this.FullPath,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };
            IEnumerable<string> allArgs = new string[] { command }.Concat(args ?? Array.Empty<string>());
#if NETFRAMEWORK
            startInfo.Arguments = string.Join(" ", allArgs);
#else
            foreach (var v in allArgs)
                startInfo.ArgumentList.Add(v);
#endif

            using var p = Process.Start(startInfo);

            if (p == null)
                throw new GitExecCommandException($"Unable to start 'git {command}' operation");

            string? outputText = null;
            string? errorText = null;

            p.OutputDataReceived += (sender, e) => outputText += e.Data + '\n';
            p.ErrorDataReceived += (sender, e) => errorText += e.Data + '\n';

            if (!string.IsNullOrEmpty(stdinText))
                p.StandardInput.Write(stdinText);

            p.StandardInput.Close();
            p.BeginErrorReadLine();
            p.BeginOutputReadLine();

            await p!.WaitForExitAsync();

            if (expectedResults != null ? !expectedResults.Contains(p.ExitCode) : p.ExitCode != 0)
                throw new GitExecCommandException($"Unexpected error {p.ExitCode} from 'git {command}' operation");

            return p.ExitCode;
        }

        internal protected async ValueTask<(int, string)> RunPlumbingCommandOut(string command, string[] args, string? stdinText = null, int[]? expectedResults = null)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo(GitConfiguration.GitProgramPath)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = this.FullPath,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };
            IEnumerable<string> allArgs = new string[] { command }.Concat(args ?? Array.Empty<string>());
#if NETFRAMEWORK
            startInfo.Arguments = string.Join(" ", allArgs.Select(x => EscapeCommandlineArgument(x)));
#else
            foreach (var v in allArgs)
                startInfo.ArgumentList.Add(v);
#endif

            using var p = Process.Start(startInfo);

            if (p == null)
                throw new GitExecCommandException($"Unable to start 'git {command}' operation");

            string outputText = "";
            string errorText = "";

            p.OutputDataReceived += (sender, e) => outputText += e.Data + '\n';
            p.ErrorDataReceived += (sender, e) => errorText += e.Data + '\n';

            if (!string.IsNullOrEmpty(stdinText))
                p.StandardInput.Write(stdinText);

            p.StandardInput.Close();
            p.BeginErrorReadLine();
            p.BeginOutputReadLine();

            await p.WaitForExitAsync();

            if (expectedResults != null ? !expectedResults.Contains(p.ExitCode) : p.ExitCode != 0)
                throw new GitExecCommandException($"Unexpected error {p.ExitCode} from 'git {command}' operation");

            return (p.ExitCode, outputText);
        }

#if NETFRAMEWORK
        static string EscapeCommandlineArgument(string argument)
        {
            if (string.IsNullOrEmpty(argument))
                return "";

            bool escape = false;
            for (int i = 0; i < argument.Length; i++)
            {
                if (char.IsWhiteSpace(argument, i))
                {
                    escape = true;
                    break;
                }
                else if (argument[i] == '\"')
                {
                    escape = true;
                    break;
                }
            }

            if (!escape)
                return argument;

            StringBuilder sb = new StringBuilder(argument.Length + 5);

            sb.Append('\"');

            for (int i = 0; i < argument.Length; i++)
            {
                switch (argument[i])
                {
                    case '\"':
                        sb.Append('\"');
                        break;
                }

                sb.Append(argument[i]);
            }

            sb.Append('\"');

            return sb.ToString();
        }
#endif

        internal protected async ValueTask<(int, string, string)> RunPlumbingCommandErr(string command, string[] args, string? stdinText = null, int[]? expectedResults = null)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo(GitConfiguration.GitProgramPath)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = this.FullPath,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };
            IEnumerable<string> allArgs = new string[] { command }.Concat(args ?? Array.Empty<string>());
#if NETFRAMEWORK
            startInfo.Arguments = string.Join(" ", allArgs);
#else
            foreach (var v in allArgs)
                startInfo.ArgumentList.Add(v);
#endif

            using var p = Process.Start(startInfo);
            string outputText = "";
            string errorText = "";

            p.OutputDataReceived += (sender, e) => outputText += e.Data + '\n';
            p.ErrorDataReceived += (sender, e) => errorText += e.Data + '\n';

            if (p == null)
                throw new GitExecCommandException($"Unable to start 'git {command}' operation");

            if (!string.IsNullOrEmpty(stdinText))
                p.StandardInput.Write(stdinText);

            p.StandardInput.Close();
            p.BeginErrorReadLine();
            p.BeginOutputReadLine();

            await p!.WaitForExitAsync();

            if (expectedResults != null ? !expectedResults.Contains(p.ExitCode) : p.ExitCode != 0)
                throw new GitExecCommandException($"Unexpected error {p.ExitCode} from 'git {command}' operation");

            return (p.ExitCode, outputText, errorText);
        }
    }
}
