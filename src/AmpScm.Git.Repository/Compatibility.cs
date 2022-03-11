using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#if NETFRAMEWORK
namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
    internal sealed class NotNullWhenAttribute : Attribute
    {
        public bool ReturnValue { get; }
        public NotNullWhenAttribute(bool returnValue)
        {
            ReturnValue = returnValue;
        }
    }
}
#endif
