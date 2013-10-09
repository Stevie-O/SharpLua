using System;

using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace SharpLua
{
    partial class Lua
    {
        public static void exit(int status)
        {
#if WindowsCE
            TerminateProcess(GetCurrentProcess(), status);
#else
            Environment.Exit(status);
#endif
        }

        public static CharPtr getenv(CharPtr envname)
        {
            // todo: fix this - mjf
            //if (envname == "LUA_PATH)
            //return "MyPath";
            return getenv(envname.ToString());
        }
        
        public static string getenv(string name)
        {
#if WindowsCE
            return null;
#else
            return Environment.GetEnvironmentVariable(name);
#endif
        }
    }
}
