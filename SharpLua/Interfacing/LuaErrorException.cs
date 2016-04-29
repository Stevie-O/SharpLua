using System;
using System.Collections.Generic;
using System.Text;

namespace SharpLua
{
    /// <summary>
    /// Wraps a non-Exception object (usually a string) to be passed to ObjectTranslator.throwError.
    /// </summary>
    internal class LuaErrorException : Exception
    {
        readonly object _luaErrorObject;
        public object LuaErrorObject { get { return _luaErrorObject; } }
        public LuaErrorException(object errorObject)
        {
            _luaErrorObject = errorObject;
        }
    }
}
