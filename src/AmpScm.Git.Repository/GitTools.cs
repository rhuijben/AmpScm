using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git
{
    public static class GitTools
    {
        public static string GetNormalizedFullPath(string path)
        {
            path = Path.GetFullPath(path);

            if (File.Exists(path))
            {
                path = Directory.GetFiles(Path.GetDirectoryName(path)!, Path.GetFileName(path)).Single();
            }
            else if (Directory.Exists(path))
            {
                path = Directory.GetDirectories(Path.GetDirectoryName(path)!, Path.GetFileName(path)).Single();
            }

            string? dir = Path.GetDirectoryName(path)!;

            for(string parent = Path.GetDirectoryName(dir)!; parent != null && parent != dir; parent = Path.GetDirectoryName(parent)!)
            {
                var p = Directory.GetDirectories(parent!, Path.GetFileName(dir)).FirstOrDefault();

                if (p != null && (!path.StartsWith(p, StringComparison.Ordinal) || path[p.Length] == Path.DirectorySeparatorChar))
                {
                    path = p + path.Substring(dir.Length);
                }
            }
            while (dir != (dir = Path.GetDirectoryName(dir)));

            if (path.Length > 2 && path[1] == ':' && char.IsLower(path, 0))
                path = char.ToUpperInvariant(path[0]) + path.Substring(1);

            return path;
        }

        public static string? FirstLine(string? message)
        {
            if (message == null)
                return null;

            return message.Split(new char[] { '\n' }, 2)[0].Trim();
        }

        internal static string? CreateSummary(string? message)
        {
            if (message == null)
                return null;
            
            if (message.Contains('\r'))
                message = message.Replace("\r", "");

            var lines = message.Split(new char[] { '\n' }, 4);

            int st;
            for(st = 0; st < lines.Length; st++)
            {
                if (string.IsNullOrWhiteSpace(lines[st]))
                    break;
            }

            return  string.Join("\n", lines.Take(st));
        }        
    }
}
