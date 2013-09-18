using System;
using System.Collections.Generic;
using System.Text;

namespace SharpLua
{
    /// <summary>
    /// Interface for an object that can parse the values on the stack 
    /// </summary>
    public interface IReturnListHandler
    {
        /// <summary>
        /// Implement and return the number of return values expected, or -1 if a variable number of results is acceptable.
        /// </summary>
        int NumResults { get; }

        /// <summary>
        /// Implement to parse the number of return values on the stack.
        /// </summary>
        /// <param name="L">Lua instance</param>
        /// <param name="stktop">Stack index where the first result variable is stored.</param>
        /// <returns></returns>
        /// <remarks>
        /// This method *will* be invoked even if NumResults is 0.
        /// </remarks>
        object[] PopValues(Lua.LuaState L, int stktop);
    }
}
