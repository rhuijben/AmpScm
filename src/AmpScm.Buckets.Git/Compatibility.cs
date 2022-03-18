using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.ComponentModel;

#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static class IsExternalInit { }
}
#endif

namespace AmpScm.Buckets.Git
{
    //internal class Compatibility
    //{
    //}
}
