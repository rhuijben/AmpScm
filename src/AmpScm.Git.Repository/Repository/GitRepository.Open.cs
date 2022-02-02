using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git
{
    public partial class GitRepository
    {        
        public static GitRepository Open(string path)
            => Open(path, true);

        public static GitRepository Open(string path, bool findGitRoot)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException("path");

            string rootDir;

            if (findGitRoot)
                rootDir = FindGitRoot(path) ?? path;
            else
                rootDir = path;

            string gitDir = Path.Combine(rootDir, ".git");
            bool bareCheck = false;

            if (!Directory.Exists(gitDir) || !File.Exists(Path.Combine(gitDir, "config")))
            {
                gitDir = rootDir;

                if (!(File.Exists(Path.Combine(gitDir, "config")) && File.Exists(Path.Combine(gitDir, "HEAD"))))
                    throw new GitRepositoryException($"Git repository not found at '{gitDir}'");

                bareCheck = true;
            }

            return new GitRepository(rootDir, bareCheck: bareCheck);
        }

        public static string? FindGitRoot(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            string? p = path;

            while(p?.Length >0)
            {
                var tst = Path.Combine(p, ".git");

                if (Directory.Exists(tst) && File.Exists(Path.Combine(tst, "config")))
                    return p;

                p = Path.GetDirectoryName(p);
            }

            return null;
        }
    }
}
