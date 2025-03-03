﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Client;
using AmpScm.Buckets.Git;
using AmpScm.Git.Repository.Implementation;

namespace AmpScm.Git.Repository
{
#pragma warning disable CA1308 // Normalize strings to uppercase
    public class GitConfiguration
    {
        protected GitRepository Repository { get; }

        readonly string _gitDir;
        bool _loaded;
        int _repositoryFormatVersion;
        readonly Dictionary<(string, string?, string), string> _config = new Dictionary<(string, string?, string), string>();

        static readonly Lazy<string> _gitExePath = new Lazy<string>(GetGitExePath, true);
        static readonly Lazy<string> _homeDir = new Lazy<string>(GetHomeDirectory, true);
        public static string GitProgramPath => _gitExePath.Value;
        public static string UserHomeDirectory => _homeDir.Value;

        internal GitConfiguration(GitRepository gitRepository, string gitDir)
        {
            Repository = gitRepository;
            _gitDir = gitDir;
            _lazy = new Lazy<GitLazyConfig>(() => new GitLazyConfig(this));
        }

        internal async ValueTask LoadAsync()
        {
            if (_loaded) return;

            foreach (string path in GetGitConfigurationFilePaths())
            {
                await LoadConfigAsync(path).ConfigureAwait(false);
            }

            try
            {
                await LoadConfigAsync(Path.Combine(_gitDir, "config")).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                throw new GitRepositoryException($"Can't open repository config '{Path.Combine(_gitDir, "config")}', GitDir='{Repository.GitDir}', FullPath='{Repository.FullPath}'", e);
            }
        }

        async ValueTask LoadConfigAsync(string path)
        {
            using var b = FileBucket.OpenRead(path);
            using var cr = new GitConfigurationReaderBucket(b);

            while (await cr.ReadConfigItem().ConfigureAwait(false) is GitConfigurationItem item)
            {
                _config[(item.Group, item.SubGroup, item.Key)] = item.Value ?? "\xFF";

                if (item.Group == "core" || item.Group == "extension")
                    ParseCore(item);
                else if (item.Group == "include")
                    await ParseInclude(path, item).ConfigureAwait(false);
                else if (item.Group == "includeif")
                    await ParseIncludeIfAsync(path, item).ConfigureAwait(false);
            }
            _loaded = true;
        }

        private async ValueTask ParseInclude(string path, GitConfigurationItem item)
        {
            if (!(item.SubGroup is var check) || string.IsNullOrEmpty(check))
                return;
            else if (item.Key != "path")
                return; // No other types documented yet

            string newPath = Path.Combine(Path.GetDirectoryName(path)!, ApplyHomeDir(item.Value!));

            if (!string.IsNullOrEmpty(newPath) && File.Exists(newPath))
            {
                await LoadConfigAsync(Path.GetFullPath(newPath)).ConfigureAwait(false);
            }
        }

        private async ValueTask ParseIncludeIfAsync(string path, GitConfigurationItem item)
        {
            if (!(item.SubGroup is var check) || string.IsNullOrEmpty(check))
                return;
            else if (item.Key != "path" || item.Value == null)
                return; // No other types documented yet

            bool caseInsensitive = false;
            if (check!.StartsWith("gitdir:", StringComparison.Ordinal))
            { }
            else if (check.StartsWith("gitdir/i:", StringComparison.Ordinal))
            {
                caseInsensitive = true;
                check = check.Remove(6, 2);
            }

            string dir = ApplyHomeDir(check.Substring(7).Trim());

            if (GitGlob.Match(dir, Repository.FullPath, GitGlobFlags.ParentPath | (caseInsensitive ? GitGlobFlags.CaseInsensitive : GitGlobFlags.None)))
            {
                string newPath = Path.Combine(Path.GetDirectoryName(path)!, ApplyHomeDir(item.Value!));

                if (!string.IsNullOrEmpty(newPath) && File.Exists(newPath))
                {
                    await LoadConfigAsync(Path.GetFullPath(newPath)).ConfigureAwait(false);
                }
            }
        }

        static string ApplyHomeDir(string path)
        {
            if (path != null && path.StartsWith("~", StringComparison.Ordinal)
                && UserHomeDirectory is var homeDir && !string.IsNullOrWhiteSpace(homeDir))
            {
                if (path.StartsWith("~/", StringComparison.Ordinal))
                    path = homeDir!.TrimEnd(Path.DirectorySeparatorChar) + path.Substring(1);
                else if (char.IsLetterOrDigit(path, 1))
                    path = Path.GetDirectoryName(homeDir) + path.Substring(1); // Might need more work on linux, but not common
            }
            return path!;
        }

        private void ParseCore(GitConfigurationItem item)
        {
            if (item.Key == "repositoryformatversion" && item.Group == "core")
            {
                if (int.TryParse(item.Value, out var version))
                    _repositoryFormatVersion = version;
            }
        }

        internal IEnumerable<(string, string)> GetGroup(string group, string? subGroup)
        {
            if (!_loaded)
                LoadAsync().AsTask().GetAwaiter().GetResult();

            group = group.ToLowerInvariant();

            foreach (var v in _config)
            {
                var (g, s, k) = v.Key;

                if (group == g && subGroup == s)
                    yield return (k, v.Value);
            }
        }

        public IEnumerable<string> GetSubGroups(string group)
        {
            if (string.IsNullOrEmpty(group))
                throw new ArgumentNullException(nameof(group));

            if (!_loaded)
                LoadAsync().AsTask().GetAwaiter().GetResult();

            group = group.ToLowerInvariant();
            HashSet<string> subGroups = new HashSet<string>();

            foreach (var v in _config)
            {
                var (g, s, _) = v.Key;

                if (s == null)
                    continue;

                if (group == g)
                {
                    if (!subGroups.Contains(s))
                    {
                        yield return s;
                        subGroups.Add(s);
                    }
                }
            }
        }

        internal async ValueTask<GitRemote?> GetRemoteAsync(string name)
        {
            if (await GetStringAsync("remote." + name, "url").ConfigureAwait(false) is string v)
            {
                return new GitRemote(Repository, name, v);
            }

            return null;
        }

        internal async IAsyncEnumerable<GitRemote> GetAllRemotes()
        {
            HashSet<string> names = new HashSet<string>();

            await LoadAsync().ConfigureAwait(false);

            foreach (var v in _config)
            {
                var (g, s, k) = v.Key;

                if (g != "remote" || s is null)
                    continue;

                if (!names.Contains(s))
                {
                    yield return new GitRemote(Repository, s, (k == "url") ? v.Value : null);
                    names.Add(s);
                }
            }
        }


        public async ValueTask<int?> GetIntAsync(string group, string key)
        {
            if (string.IsNullOrEmpty(group))
                throw new ArgumentNullException(nameof(group));

            await LoadAsync().ConfigureAwait(false);

            int n = group.IndexOf('.', StringComparison.Ordinal);
            string? subGroup = (n > 0) ? group.Substring(n + 1) : null;
            group = ((n > 0) ? group.Substring(0, n) : group).ToLowerInvariant();

            if (_config.TryGetValue((group, subGroup, key), out var vResult)
                && int.TryParse(vResult, out var r))
            {
                return r;
            }
            else
                return null;
        }

        internal int? GetInt(string group, string key)
        {
            return GetIntAsync(group, key).AsTask().Result;
        }

        public async ValueTask<string?> GetStringAsync(string group, string key)
        {
            if (string.IsNullOrEmpty(group))
                throw new ArgumentNullException(nameof(group));

            await LoadAsync().ConfigureAwait(false);

            int n = group.IndexOf('.', StringComparison.Ordinal);
            string? subGroup = (n > 0) ? group.Substring(n + 1) : null;
            group = ((n > 0) ? group.Substring(0, n) : group).ToLowerInvariant();

            if (_config.TryGetValue((group, subGroup, key), out var vResult))
            {
                if (vResult == "\xFF")
                    return "";
                return vResult;
            }
            else
                return null;
        }

        internal string? GetString(string group, string key)
        {
            return GetStringAsync(group, key).AsTask().Result;
        }


        public async ValueTask<bool?> GetBoolAsync(string group, string key)
        {
            if (string.IsNullOrEmpty(group))
                throw new ArgumentNullException(nameof(group));

            await LoadAsync().ConfigureAwait(false);

            int n = group.IndexOf('.', StringComparison.Ordinal);
            string? subGroup = (n > 0) ? group.Substring(n + 1) : null;
            group = ((n > 0) ? group.Substring(0, n) : group).ToLowerInvariant();

            if (_config.TryGetValue((group, subGroup, key), out var vResult))
            {
                // As generated by 'git init'
                if (string.Equals(vResult, "true", StringComparison.OrdinalIgnoreCase))
                    return true;
                else if (string.Equals(vResult, "false", StringComparison.OrdinalIgnoreCase))
                    return false;

                // The simple no value cases
                else if (vResult == "\xFF" || vResult is null)
                    return true;
                else if (vResult.Length == 0)
                    return false;

                // And other documented ok
                else if (string.Equals(vResult, "yes", StringComparison.OrdinalIgnoreCase))
                    return true;
                else if (string.Equals(vResult, "on", StringComparison.OrdinalIgnoreCase))
                    return true;
                else if (string.Equals(vResult, "1", StringComparison.OrdinalIgnoreCase))
                    return true;
                else if (string.Equals(vResult, "\xFF", StringComparison.OrdinalIgnoreCase))
                    return true;
                else if (string.Equals(vResult, "no", StringComparison.OrdinalIgnoreCase))
                    return false;
                else if (string.Equals(vResult, "off", StringComparison.OrdinalIgnoreCase))
                    return false;

                else if (string.Equals(vResult, "0", StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            return null;
        }

        public bool? GetBool(string group, string key)
        {
            return GetBoolAsync(group, key).AsTask().Result;
        }

        internal class GitLazyConfig
        {
            GitConfiguration Configuration { get; }
            readonly Lazy<bool> _repositoryIsLazy;
            readonly Lazy<bool> _repositoryIsShallow;
            readonly Lazy<bool> _repositoryCommitGraph;
            readonly Lazy<bool> _repositorySupportsMultiPack;
            readonly Lazy<int> _autoGCBlobs;

            public GitLazyConfig(GitConfiguration config)
            {
                Configuration = config ?? throw new ArgumentNullException(nameof(config));

                _repositoryIsLazy = new Lazy<bool>(GetRepositoryIsLazy);
                _repositoryIsShallow = new Lazy<bool>(GetRepositoryIsShallow);
                _repositoryCommitGraph = new Lazy<bool>(GetRepositoryCommitGraph);
                _repositorySupportsMultiPack = new Lazy<bool>(GetRepositorySupportsMultiPack);
                _autoGCBlobs = new Lazy<int>(GetAutGCBlobs);
            }

            bool GetRepositoryIsLazy()
            {
                if (Configuration._loaded && Configuration._repositoryFormatVersion == 0)
                    return false;

                foreach (var v in Configuration.GetSubGroups("remote"))
                {
                    if (Configuration.GetBool("remote." + v, "promisor") ?? false)
                        return true;
                }

                return false;
            }

            bool GetRepositoryIsShallow()
            {
                return File.Exists(Path.Combine(Configuration.Repository.GitDir, "shallow"));
            }

            bool GetRepositoryCommitGraph()
            {
                return Configuration.GetBool("core", "commitGraph") ?? true; // By default enabled in git current
            }

            bool GetRepositorySupportsMultiPack()
            {
                return Configuration.GetBool("core", "multiPackIndex") ?? true; // By default enabled in git current
            }

            int GetAutGCBlobs()
            {
                return Configuration.GetInt("gc", "auto") ?? 6700;
            }

            public bool RepositoryIsLazy => _repositoryIsLazy.Value;
            public bool RepositoryIsShallow => _repositoryIsShallow.Value;

            public bool CommitGraph => _repositoryCommitGraph.Value;
            public bool MultiPack => _repositorySupportsMultiPack.Value;

            public int AutoGCBlobs => _autoGCBlobs.Value;
        }

        readonly Lazy<GitLazyConfig> _lazy;

        internal GitLazyConfig Lazy => _lazy.Value;

        static string GetGitExePath()
        {
            return GitExePathLook() ?? GetExePathWhere() ?? null!;
        }

        private static string? GetExePathWhere()
        {
            try
            {
                var psi = new ProcessStartInfo(Environment.NewLine == "\n" ? "which" : "where", "git")
                {
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                };

                string outputText = "";
                using var ps = Process.Start(psi);

                if (ps == null)
                    return null;
                ps.StandardInput.Close();
                ps.OutputDataReceived += (sender, e) => outputText += e.Data;
                ps.ErrorDataReceived += (sender, e) => { };
                ps.BeginErrorReadLine();
                ps.BeginOutputReadLine();
                if (ps.WaitForExit(100) && ps.ExitCode == 0)
                {
                    string git = outputText.Split(new[] { '\n' }, 2)[0].Trim();

                    if (File.Exists(git)
                        && File.Exists(git = Path.GetFullPath(git)))
                    {
                        return git;
                    }
                }

                return null;
            }
            catch (InvalidOperationException)
            { }
            catch (IOException)
            { }
            catch (System.ComponentModel.Win32Exception)
            { }
            return null;
        }

        private static string? GitExePathLook()
        {
            try
            {
                string? path = Environment.GetEnvironmentVariable("PATH");

                if (path == null)
                    return null;

                foreach (var p in path.Split(Path.PathSeparator))
                {
                    try
                    {
                        string git;
                        if (File.Exists(git = Path.Combine(p, "git")))
                            return Path.GetFullPath(git);
                        else if (File.Exists(git = Path.Combine(p, "git.exe")))
                            return Path.GetFullPath(git);
                    }
                    catch (ArgumentException)
                    { }
                    catch (IOException)
                    { }
                    catch (SecurityException)
                    { }
                }
            }
            catch (ArgumentException)
            { }
            catch (IOException)
            { }
            catch (SecurityException)
            { }
            return null;
        }

        public GitSignature Identity
        {
            get
            {
                var username = GetString("user", "name") ?? Environment.UserName ?? "Someone";
                var email = GetString("user", "email") ?? $"me@{Environment.MachineName}.local";

                return new GitSignature(username, email, DateTime.Now);
            }
        }

        static readonly object _extraHeaderSetTag = new();
#pragma warning disable CA2109 // Review visible event handlers
        public void BasicAuthenticationHandler(object? sender, BasicBucketAuthenticationEventArgs e)
#pragma warning restore CA2109 // Review visible event handlers
        {
            if (e is null)
                throw new ArgumentNullException(nameof(e));

            if (e.Items[_extraHeaderSetTag] is null && (e.Uri?.Scheme.StartsWith("http", StringComparison.OrdinalIgnoreCase) ?? false))
            {
                e.Items[_extraHeaderSetTag] = _extraHeaderSetTag;

                // GitHub action uses $ git.exe config --local http.https://github.com/.extraheader "AUTHORIZATION: basic ***"
                var extraHeader = GetString($"http.{e.Uri.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped)}/", "extraheader") ?? GetString($"http", "extraheader");

                if (!string.IsNullOrEmpty(extraHeader) && extraHeader.StartsWith("Authorization: Basic ", StringComparison.OrdinalIgnoreCase))
                {
                    var p = extraHeader.Split(new char[] { ' ' }, 3)[2];

                    try
                    {
                        var userPass = Encoding.UTF8.GetString(Convert.FromBase64String(p));

                        string[] parts = userPass.Split(new char[] { ':' }, 2);

                        e.Username = parts[0];
                        e.Password = parts[1];
                        e.Continue = true; // If failed, fall through in next code
                        e.Handled = true;
                        return;
                    }
                    catch (FormatException)
                    {
                        // Fall through to normal auth
                    }
                    catch (DecoderFallbackException)
                    {
                        // Fall through to normal auth
                    }
                    catch (ArgumentException)
                    {
                        // Fall through to normal auth
                    }
                }
            }

            e.Continue = false; // Only run this handler once!

            // BUG: Somehow the first line gets corrupted, so we write an ignored first line to make sure the required fields get through correctly

            var r = Repository.RunPlumbingCommandOut("credential", new[] { "fill" }, stdinText: $"url={e.Uri}").AsTask();

            var (exitCode, output) = r.GetAwaiter().GetResult();
            bool gotUser = false;
            bool gotPass = false;
            string? username = null;
            string? password = null;

            foreach (var l in output.Split('\n'))
            {
                var kv = l.Split(new[] { '=' }, 2);

                if ("username".Equals(kv[0], StringComparison.OrdinalIgnoreCase))
                {
                    username = kv[1].TrimEnd();
                    gotUser = true;
                }
                else if ("password".Equals(kv[0], StringComparison.OrdinalIgnoreCase))
                {
                    password = kv[1].TrimEnd();
                    gotPass = true;
                }
            }

            if (!gotUser || !gotPass || (string.IsNullOrEmpty(username) && string.IsNullOrEmpty(password)))
            {
                e.Handled = false;
            }
            else
            {
                e.Username = username;
                e.Password = password;
                e.Succeeded += async (_, _) => await Repository.RunPlumbingCommand("credential", new[] { "approve" }, stdinText: $"url={e.Uri}\nusername={username}\npassword={password}\n").ConfigureAwait(false);
                e.Failed += async (_, _) => await Repository.RunPlumbingCommand("credential", new[] { "reject" }, stdinText: $"url={e.Uri}\nusername={username}\npassword={password}\n").ConfigureAwait(false);
            }
        }

        public static IEnumerable<string> GetGitConfigurationFilePaths(bool includeSystem = true)
        {
            string f;
            if (includeSystem && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GIT_CONFIG_NOSYSTEM")))
            {
                if (Environment.GetEnvironmentVariable("GIT_CONFIG_SYSTEM") is var gitConfigSystem
                    && !string.IsNullOrWhiteSpace(gitConfigSystem) && File.Exists(gitConfigSystem))
                {
                    yield return Path.GetFullPath(gitConfigSystem);
                }
                else if (GitProgramPath != null)
                {
                    string dir = Path.GetDirectoryName(GitProgramPath)!;

                    if (Path.GetDirectoryName(dir) is var parent && File.Exists(f = Path.Combine(parent!, "etc", "gitconfig")))
                        yield return Path.GetFullPath(f);
                    else if (Path.GetDirectoryName(parent) is var parent2 && File.Exists(f = Path.Combine(parent2!, "etc", "gitconfig")))
                        yield return Path.GetFullPath(f);

                }
                else if (includeSystem && Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) is var programFiles)
                {
                    if (File.Exists(f = Path.Combine(programFiles, "git", "etc", "gitconfig")))
                        yield return Path.GetFullPath(f);
                }

                if (includeSystem && Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) is var commonAppData)
                {
                    if (File.Exists(f = Path.Combine(commonAppData, "git", "gitconfig")))
                        yield return Path.GetFullPath(f);
                }
            }

            if (Environment.GetEnvironmentVariable("GIT_CONFIG_GLOBAL") is var gitConfigGlobal
                    && !string.IsNullOrWhiteSpace(gitConfigGlobal) && File.Exists(gitConfigGlobal))
            {
                yield return Path.GetFullPath(gitConfigGlobal);
            }
            else if (UserHomeDirectory is string home && !string.IsNullOrWhiteSpace(UserHomeDirectory) && File.Exists(f = Path.Combine(home, ".gitconfig")))
            {
                yield return Path.GetFullPath(f);
            }
            else if (Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) is var localAppData
                && File.Exists(f = Path.Combine(localAppData, "git", "gitconfig")))
            {
                yield return Path.GetFullPath(f);
            }
            else if (Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) is var appData
                && File.Exists(f = Path.Combine(appData, "git", "gitconfig")))
            {
                yield return Path.GetFullPath(f);
            }

        }

        static string GetHomeDirectory()
        {
            if (Environment.GetEnvironmentVariable("HOME") is string home
                && !string.IsNullOrWhiteSpace(home) && Directory.Exists(home))
            {
                return Path.GetFullPath(home);
            }

            if (Environment.GetEnvironmentVariable("HOMEDRIVE") is var homeDrive
                && Environment.GetEnvironmentVariable("HOMEPATH") is var homePath
                && !string.IsNullOrWhiteSpace(homeDrive) && !string.IsNullOrWhiteSpace(homePath))
            {
                homeDrive += "\\";
                if (homePath!.StartsWith("\\", StringComparison.Ordinal)
                    || homePath.StartsWith("/", StringComparison.Ordinal))
                {
                    homePath = homeDrive + homePath;
                }

                if (Directory.Exists(home = Path.Combine(homeDrive, homePath!)))
                    return Path.GetFullPath(home);
            }

            if (Environment.GetEnvironmentVariable("USERPROFILE") is var userProfile
                && !string.IsNullOrEmpty(userProfile) && Directory.Exists(userProfile))
            {
                return userProfile;
            }

            return null!;
        }
    }
}
