using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Amp.Git
{
    public partial class GitRepository
    {        
        public static GitRepository Init(string path)
            => Init(path, false);

        public static GitRepository Init(string path, bool isBare)
        {
            if (Directory.Exists(path) && (Directory.GetFiles(path).Any() || Directory.GetDirectories(path).Any()))
                throw new GitRepositoryException($"{path} already exists");

            // Quick and dirty setup minimal git repository
            string gitDir = path;
            if (!isBare)
            {
                gitDir = Path.Combine(path, ".git");
            }
            
            Directory.CreateDirectory(Path.Combine(gitDir, "hooks"));
            Directory.CreateDirectory(Path.Combine(gitDir, "info"));
            Directory.CreateDirectory(Path.Combine(gitDir, "objects/info"));
            Directory.CreateDirectory(Path.Combine(gitDir, "objects/pack"));
            Directory.CreateDirectory(Path.Combine(gitDir, "refs/heads"));
            Directory.CreateDirectory(Path.Combine(gitDir, "refs/tags"));

            File.WriteAllText(Path.Combine(gitDir, "description"), "Unnamed repository; edit this file 'description' to name the repository." + Environment.NewLine);
            File.WriteAllText(Path.Combine(gitDir, "HEAD"), "ref: refs/heads/master\n");
            string configText = ""
                + "[core]\n"
                + "\trepositoryformatversion = 0\n"
                + "\tfilemode = false\n"
                + "\tbare = false\n"
                + "\tlogallrefupdates = true\n"
                + "\tsymlinks = false\n"
                + "\tignorecase = true\n";

            if (isBare)
                configText = configText.Replace("\tbare = false", "\tbare = true");

            if (Environment.NewLine != "\r\n")
                configText = configText.Replace("\tignorecase = true\n", "");

            File.WriteAllText(Path.Combine(gitDir, "config"), configText);

            File.WriteAllText(Path.Combine(gitDir, "info/exclude"), ""
                + "# git ls-files --others --exclude-from=.git/info/exclude\n"
                + "# Lines that start with '#' are comments.\n"
                + "# For a project mostly in C, the following would be a good set of\n"
                + "# exclude patterns (uncomment them if you want to use them):\n"
                + "# *.[oa]\n"
                + "# *~\n"
            );

            return new GitRepository(path, bare: isBare);
        }

    }
}
