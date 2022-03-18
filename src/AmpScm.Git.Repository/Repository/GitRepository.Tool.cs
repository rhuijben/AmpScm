using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AmpScm.Git.Implementation;
using AmpScm.Git.Repository;

namespace AmpScm.Git
{
    partial class GitRepository
    {
        protected internal ValueTask<int> RunPlumbingCommand(string command, params string[] args)
        {
            return RunPlumbingCommand(command, args, stdinText: null, expectedResults: null);
        }

        protected internal async ValueTask<int> RunPlumbingCommand(string command, string[] args, string? stdinText = null, int[]? expectedResults = null)
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

            StringBuilder? outputText = null;
            StringBuilder? errorText = null;

            p.OutputDataReceived += (sender, e) => { lock (startInfo) (outputText ??= new StringBuilder()).AppendLine(e.Data); };
            p.ErrorDataReceived += (sender, e) => { lock (startInfo) (errorText ??= new StringBuilder()).AppendLine(e.Data); };

            if (!string.IsNullOrEmpty(stdinText))
                p.StandardInput.Write(stdinText);

            p.StandardInput.Close();
            p.BeginErrorReadLine();
            p.BeginOutputReadLine();

            await p!.WaitForExitAsync().ConfigureAwait(false);

            if (expectedResults != null ? !expectedResults.Contains(p.ExitCode) : p.ExitCode != 0)
                throw new GitExecCommandException($"Unexpected error {p.ExitCode} from 'git {command}' operation: {errorText}");

            return p.ExitCode;
        }

        protected internal async ValueTask<(int ExitCode, string OutputText)> RunPlumbingCommandOut(string command, string[] args, string? stdinText = null, int[]? expectedResults = null)
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

            StringBuilder? outputText = null;
            StringBuilder? errorText = null;

            p.OutputDataReceived += (sender, e) => { lock (startInfo) (outputText ??= new StringBuilder()).AppendLine(e.Data); };
            p.ErrorDataReceived += (sender, e) => { lock (startInfo) (errorText ??= new StringBuilder()).AppendLine(e.Data); };

            p.BeginErrorReadLine();
            p.BeginOutputReadLine();

            if (!string.IsNullOrEmpty(stdinText))
            {
                p.StandardInput.Write(stdinText);
            }

            p.StandardInput.Close();

            await p.WaitForExitAsync().ConfigureAwait(false);

            if (expectedResults != null ? !expectedResults.Contains(p.ExitCode) : p.ExitCode != 0)
                throw new GitExecCommandException($"Unexpected error {p.ExitCode} from 'git {command}' operation: {errorText}");

            lock (startInfo)
            {
                return (p.ExitCode, outputText?.ToString() ?? "");
            }
        }

        protected internal IAsyncEnumerable<string> WalkPlumbingCommand(string command, string[] args, string? stdinText = null, int[]? expectedResults = null)
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

            var p = Process.Start(startInfo);

            if (p == null)
                throw new GitExecCommandException($"Unable to start 'git {command}' operation");

            if (!string.IsNullOrEmpty(stdinText))
                p.StandardInput.Write(stdinText);

            p.StandardInput.Close();

            return new StdOutputWalker(p, expectedResults);
        }

        sealed class StdOutputWalker : IAsyncEnumerable<string>, IAsyncEnumerator<string>
        {
            readonly Process _p;
            readonly StreamReader _reader;
            bool _eof;
            string? _current;
            StringBuilder? _errText;
            readonly int[]? _expectedResults;

            public StdOutputWalker(Process p, int[]? expectedResults)
            {
                if (p is null)
                    throw new ArgumentNullException(nameof(p));

                _p = p;
                _expectedResults = expectedResults;
                _reader = p.StandardOutput;
                _p.ErrorDataReceived += P_ErrorDataReceived;
            }

            public string Current => _current!;

            public ValueTask DisposeAsync()
            {
                _p.Dispose();
                return default;
            }

            public IAsyncEnumerator<string> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            {
                return this;
            }

            public async ValueTask<bool> MoveNextAsync()
            {
                if (_errText is not null)
                    throw new GitExecCommandException(_errText.ToString());
                if (_eof)
                    return false;

                _current = await _reader.ReadLineAsync().ConfigureAwait(false);

                if (_current is null)
                {
                    _eof = true;
                    await _p.WaitForExitAsync().ConfigureAwait(false);

                    if (_expectedResults != null ? !_expectedResults.Contains(_p.ExitCode) : _p.ExitCode != 0)
                        throw new GitExecCommandException($"Unexpected error {_p.ExitCode} from git plumbing operation: {_errText}");

                    _p.Dispose();

                    return false;
                }

                return true;
            }

            private void P_ErrorDataReceived(object sender, DataReceivedEventArgs e)
            {
                if (e.Data is not null)
                {
                    (_errText ??= new StringBuilder()).AppendLine(e.Data);
                }
            }
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

        protected internal async ValueTask<(int ExitCode, string OutputText, string ErrorText)> RunPlumbingCommandErr(string command, string[] args, string? stdinText = null, int[]? expectedResults = null)
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

            StringBuilder? outputText = null;
            StringBuilder? errorText = null;

            p.OutputDataReceived += (sender, e) => (outputText ??= new StringBuilder()).AppendLine(e.Data);
            p.ErrorDataReceived += (sender, e) => (errorText ??= new StringBuilder()).AppendLine(e.Data);

            if (p == null)
                throw new GitExecCommandException($"Unable to start 'git {command}' operation");

            if (!string.IsNullOrEmpty(stdinText))
                p.StandardInput.Write(stdinText);

            p.StandardInput.Close();
            p.BeginErrorReadLine();
            p.BeginOutputReadLine();

            await p!.WaitForExitAsync().ConfigureAwait(false);

            if (expectedResults != null ? !expectedResults.Contains(p.ExitCode) : p.ExitCode != 0)
                throw new GitExecCommandException($"Unexpected error {p.ExitCode} from 'git {command}' operation");

            return (p.ExitCode, outputText?.ToString() ?? "", errorText?.ToString() ?? "");
        }
    }
}
