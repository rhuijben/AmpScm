using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Sets.Walker
{
    internal static class LegacyCompat
    {
#if NETFRAMEWORK
        public static bool TryGetValue<T>(this HashSet<T> set, T value, /* [MaybeNullWhen(false)] */ out T existingValue)
            where T : class, IEquatable<T>
        {
            if (set.Contains(value))
                foreach (var v in set)
                {
                    if (v.Equals(value))
                    {
                        existingValue = v;
                        return true;
                    }
                }
            existingValue = default!;
            return false;
        }
#endif
    }
}
