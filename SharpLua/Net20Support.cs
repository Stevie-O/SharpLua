using System;
using System.Collections.Generic;
using System.Text;

/* A WHOLE LOT of the files there have a preset list of "using X" clauses, including System.Linq, that they don't actually need.
 * Rather than go through and edit every freaking file to remove "using System.Linq", I'm just adding this here to make it happy.
 */
#if NET20

namespace System.Linq
{
    class Dummy { }
}

namespace System
{
    delegate TResult Func<T, TResult>(T arg);
    delegate TResult Func<T1, T2, TResult>(T1 arg1, T2 arg2);
}

namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Method)]
    public class ExtensionAttribute : Attribute { }
}

#endif

// this was added in 4.0
namespace System
{
    /// <summary>
    /// Bare minimum implementation of Tuple for SharpLua support
    /// </summary>
    /// <typeparam name="A"></typeparam>
    /// <typeparam name="B"></typeparam>
    public class Tuple<A, B>
    {
        public A Item1 { get; set; }
        public B Item2 { get; set; }

        public Tuple() { }
        public Tuple(A item1, B item2) { this.Item1 = item1; this.Item2 = item2; }
    }
}
