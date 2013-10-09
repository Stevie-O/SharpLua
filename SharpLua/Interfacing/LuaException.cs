using System;
using System.Runtime.Serialization;

namespace SharpLua
{
    /// <summary>
    /// Exceptions thrown by the Lua runtime
    /// </summary>
    [Serializable]
    public class LuaException : Exception
    {
        public LuaException()
        { }

        public LuaException(string message)
            : base(message)
        { }

        public LuaException(string message, Exception innerException)
            : base(message, innerException)
        { }

#if WindowsCE
        public virtual string Source { get { return null; } }
#else
        protected LuaException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        { }
#endif
    }
}
