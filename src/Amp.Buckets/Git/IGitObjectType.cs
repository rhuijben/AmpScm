using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Amp.Buckets.Git
{
    public enum GitObjectType
    {
        None = 0, // Reserved. Unused

        // These types are valid objects
        Commit = 1,
        Tree = 2,
        Blob = 3,
        Tag = 4,
    };

    public interface IGitObjectType
    {
        GitObjectType Type { get; }

        ValueTask ReadTypeAsync();

        ValueTask<long?> ReadRemainingBytesAsync();
    }
}
