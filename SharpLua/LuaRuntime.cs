using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace SharpLua
{
    /// <summary>
    /// A Lua Runtime for running files and strings
    /// </summary>
    public class LuaRuntime
    {
        static LuaInterface _interface = new LuaInterface();

        /// <summary>
        /// Runs a file on the current LuaInterface object.
        /// </summary>
        /// <param name="luaFile">Path to file.</param>
        /// <param name="args">Array of arguments to pass to the file (may be null)</param>
        /// <param name="arg0_idx">Index into args where the script filename is stored.
        /// May be -1 if it's not actually even in there; should be less than args.Length - 1</param>
        /// <returns></returns>
        public static object[] RunFile(string luaFile, object[] args, int arg0_idx)
        {
            return _interface.DoFile(luaFile, args, arg0_idx);
        }

        /// <summary>
        /// Runs Lua code on the current LuaInterface object.
        /// </summary>
        /// <param name="luaCode">Lua source code.</param>
        /// <returns></returns>
        public static object[] Run(string luaCode)
        {
            return _interface.DoString(luaCode);
        }

        /// <summary>
        /// Helper method (see remarks).
        /// </summary>
        /// <param name="spath">Path and filename, possibly without extension.</param>
        /// <returns></returns>
        /// <remarks>
        /// This method attempts to auto-complete the specified path and filename with an extension.
        /// The following extensions are tried, in this order:
        /// <list type="number">
        /// <item>.lua</item>
        /// <item>.slua</item>
        /// <item>.luac</item>
        /// <item>.sluac</item>
        /// </list>
        /// The check is a simple filesystem check, so relative paths start from the current working directory.
        /// </remarks>
        public static string FindFullPath(string spath)
        {
#if WindowsCE
            spath = Lua.GetWinCePath(spath);
#endif
            if (File.Exists(spath))
                return spath;
            if (File.Exists(spath + ".lua")) // lua
                return spath + ".lua";
            if (File.Exists(spath + ".slua")) // sLua (SharpLua)
                return spath + ".slua";
            if (File.Exists(spath + ".sluac")) // sLuac (SharpLua compiled)
                return spath + ".sluac";
            if (File.Exists(spath + ".luac")) // Luac (Lua compiled)
                return spath + ".luac";
            /*if (File.Exists(spath + ".dll"))
                return spath + ".dll";
            if (File.Exists(spath + ".exe"))
                return spath + ".exe";
            */

            return spath; // let the caller handle the invalid filename
        }

        /// <summary>
        /// Prints the SharpLua Banner
        /// </summary>
        public static void PrintBanner()
        {
            string asmVer = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            Console.WriteLine("SharpLua " + asmVer + ", Copyright (C) 2011-2012 LoDC");
        }

        /// <summary>
        /// Returns the value of the specified global variable.
        /// You should capture returns in a 'dynamic' object
        /// </summary>
        /// <example>
        /// dynamic name = LuaRuntime.GetVariable("name");
        /// </example>
        /// <param name="varName">Global variable name.</param>
        /// <returns>The global variable's value.
        /// 
        /// TODO: behavior for undefined globals?
        /// </returns>
        public static object GetVariable(string varName)
        {
            return _interface[varName];
        }

        /// <summary>
        /// Sets the value of the specified global variable to <paramref name="val"/>.
        /// </summary>
        /// <param name="varName">Global variable name.</param>
        /// <param name="val">New value.</param>
        public static void SetVariable(string varName, object val)
        {
            _interface[varName] = val;
        }

        public static void RegisterModule(Type t)
        {
            _interface.RegisterModule(t);
        }

        /// <summary>
        /// Gets the global LuaInterface object used by LuaRuntime methods.
        /// </summary>
        /// <returns>The current LuaInterface object.</returns>
        public static LuaInterface GetLua()
        {
            return _interface;
        }

        /// <summary>
        /// Sets the global LuaInterface object used by LuaRuntime methods.
        /// </summary>
        /// <param name="i">New LuaInterface object.</param>
        public static void SetLua(LuaInterface i)
        {
            _interface = i;
        }

        /// <summary>
        /// Sets the global LuaInterface object used by LuaRuntime methods.
        /// </summary>
        /// <param name="lua">???</param>
        public static void SetLua(Lua.LuaState lua)
        {
            _interface = lua.Interface;
        }

        static string EscapeBackslashes(string s)
        {
            if (s == null || s.Length == 0) return s;
            if (s.IndexOf('\\') < 0) return s;
            return s.Replace("\\", "\\\\");
        }

        /// <summary>
        /// Performs "require(<paramref name="lib"/>)".
        /// </summary>
        /// <param name="lib">Path and name of library file.</param>
        public static void Require(string lib)
        {
            lib = EscapeBackslashes(lib);
            Run("require('" + lib + "')");
            
            //Lua.lua_getglobal(_interface.LuaState, "require");
            //Lua.lua_pushstring(_interface.LuaState, lib);
            //return report(L, docall(L, 1, 1));
        }
    }
}
