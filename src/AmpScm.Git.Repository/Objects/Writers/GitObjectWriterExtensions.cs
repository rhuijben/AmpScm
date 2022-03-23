using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Git.Objects;

namespace AmpScm.Git.Objects
{
    public static class GitObjectWriterExtensions
    {
        public static GitTreeWriter AsWriter(this GitTree tree)
        {
            var gtw = GitTreeWriter.CreateEmpty();

            foreach(var v in tree)
            {
                switch(v.ElementType)
                {
                    case GitTreeElementType.File:
                    case GitTreeElementType.FileExecutable:
                        gtw.Add(v.Name, ((GitBlob)v.GitObject).AsWriter());
                        break;
                    case GitTreeElementType.Directory:
                        gtw.Add(v.Name, ((GitTree)v.GitObject).AsWriter());
                        break;
                    case GitTreeElementType.SymbolicLink:
                    case GitTreeElementType.GitCommitLink:
                        throw new NotImplementedException();
                }
            }
            gtw.PutId(tree.Id); // TODO: Cleanup
            return gtw;
        }

        public static GitBlobWriter AsWriter(this GitBlob blob)
        {
            var bw = GitBlobWriter.CreateFrom(blob.GetBucket());
            bw.PutId(blob.Id); // TODO: Cleanup

            return bw;
        }

        public static GitCommitWriter AsWriter(this GitCommit commit)
        {
            var bw = GitCommitWriter.CreateFromTree(commit.Tree.AsWriter());
            bw.Parents = commit.Parents.Select(x => (x ?? throw new InvalidOperationException()).AsWriter()).ToArray();


            bw.PutId(commit.Id); // TODO: Cleanup

            return bw;
        }
    }
}
