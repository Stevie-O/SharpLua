/*
** $Id: loslib.c,v 1.19.1.3 2008/01/18 16:38:18 roberto Exp $
** Standard Operating System library
** See Copyright Notice in lua.h
*/

using System;
using System.Threading;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace SharpLua
{
	using TValue = Lua.lua_TValue;
	using StkId = Lua.lua_TValue;
	using lua_Integer = System.Int32;
	using lua_Number = System.Double;

	public partial class Lua
	{
		private static int os_pushresult (LuaState L, int i, CharPtr filename) {
		  object en = errno;  /* calls to Lua API may change this value */
		  if (i != 0) {
			lua_pushboolean(L, 1);
			return 1;
		  }
		  else {
			lua_pushnil(L);
			lua_pushfstring(L, "%s: %s", filename, strerror(en));
			lua_pushinteger(L, getinterror(en));
			return 3;
		  }
		}


		private static int os_execute (LuaState L) {
            CharPtr arg = luaL_optstring(L, 1, null);
            string exec_option;

#if XBOX || SILVERLIGHT
            if (arg == null)
                return 0;
			luaL_error(L, "os_execute not supported on XBox360/Silverlight");
            // original lua uses system(), which returns -1 on error
			return -1;
#else

            string shell;
            string formatted_args;

#if WindowsCE
            // (SMO) This was determined experimentally by mucking with a 
            // Micros Workstation 5
            shell = "\\windows\\cmd.exe";
            exec_option = "/c ";
            if (!File.Exists(shell)) shell = null;
#else
            shell = Environment.GetEnvironmentVariable("COMSPEC");
            if (shell == null)
            {
                exec_option = "-c ";
                shell = Environment.GetEnvironmentVariable("SHELL");
            }
            else
            {
                exec_option = "/s /c";
            }
#endif

            if (arg == null)
                return (shell == null) ? 0 : 1;

            if (shell == null)
            {
                luaL_error(L, "os_execute: neither COMSPEC nor SHELL defined");
                return -1;
            
            }
            string cmdline = new string(arg.chars, arg.index, strlen(arg));
            //Debug.Print("os_execute('{0}')", cmdline);

#if WindowsCE
            formatted_args = string.Concat(exec_option, cmdline);
#else
            formatted_args = string.Format(
                            "{0}\"{1}\"", exec_option, cmdline
                            );
#endif

            System.Diagnostics.Process proc = new System.Diagnostics.Process();
            proc.EnableRaisingEvents = false;
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.FileName = shell;
            proc.StartInfo.Arguments = formatted_args;
            //Debug.Print("\tArguments = '{0}'", proc.StartInfo.Arguments);
            proc.Start();
            proc.WaitForExit();
            lua_pushinteger(L, proc.ExitCode);
            return 1;
#endif
		}


		private static int os_remove (LuaState L) {
		  CharPtr filename = luaL_checkstring(L, 1);
		  int result = 1;
          string s_filename = filename.ToString();
#if WindowsCE
          s_filename = GetWinCePath(s_filename);
#endif
            try { 
              if (!File.Exists(s_filename)) { result = 0; }
              else File.Delete(s_filename);
          } catch {result = 0;}
		  return os_pushresult(L, result, filename);
		}


		private static int os_rename (LuaState L) {
		  CharPtr fromname = luaL_checkstring(L, 1);
		  CharPtr toname = luaL_checkstring(L, 2);
		  int result;
          string s_fromname = fromname.ToString();
          string s_toname = toname.ToString();
#if WindowsCE
          s_fromname = GetWinCePath(s_fromname);
          s_toname = GetWinCePath(s_toname);
#endif
          
            try
		  {
			  File.Move(s_fromname, s_toname);
			  result = 1;
		  }
		  catch
		  {
			  result = 0; // todo: this should be a proper error code
		  }
		  return os_pushresult(L, result, fromname);
		}


		private static int os_tmpname (LuaState L) {
#if XBOX
		  luaL_error(L, "os_tmpname not supported on Xbox360");
#else
          string filename = Path.GetTempFileName();
          File.Delete(filename);
		  lua_pushstring(L, filename);
#endif
		  return 1;
		}


		private static int os_getenv (LuaState L) {
		  lua_pushstring(L, getenv(luaL_checkstring(L, 1)));  /* if null push nil */
		  return 1;
		}


		private static int os_clock (LuaState L) {
		  long ticks = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
		  lua_pushnumber(L, ((lua_Number)ticks)/(lua_Number)1000);
		  return 1;
		}


		/*
		** {======================================================
		** Time/Date operations
		** { year=%Y, month=%m, day=%d, hour=%H, min=%M, sec=%S,
		**   wday=%w+1, yday=%j, isdst=? }
		** =======================================================
		*/

		private static void setfield (LuaState L, CharPtr key, int value) {
		  lua_pushinteger(L, value);
		  lua_setfield(L, -2, key);
		}

		private static void setboolfield (LuaState L, CharPtr key, int value) {
		  if (value < 0)  /* undefined? */
			return;  /* does not set field */
		  lua_pushboolean(L, value);
		  lua_setfield(L, -2, key);
		}

		private static int getboolfield (LuaState L, CharPtr key) {
		  int res;
		  lua_getfield(L, -1, key);
		  res = lua_isnil(L, -1) ? -1 : lua_toboolean(L, -1);
		  lua_pop(L, 1);
		  return res;
		}

		private static int getfield (LuaState L, CharPtr key, int d) {
		  int res;
		  lua_getfield(L, -1, key);
		  if (lua_isnumber(L, -1) != 0)
			res = (int)lua_tointeger(L, -1);
		  else {
			if (d < 0)
			  return luaL_error(L, "field " + LUA_QS + " missing in date table", key);
			res = d;
		  }
		  lua_pop(L, 1);
		  return res;
		}

        static lua_Number? nullable_checknumber(LuaState L, int narg)
        {
            return luaL_checknumber(L, narg);
        }

        static lua_Number DateTimeToEpoch(DateTime when)
        {
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            TimeSpan delta = when - epoch;
            return Math.Floor(delta.TotalSeconds);
        }

        static DateTime EpochToDateTime(lua_Number epochValue, DateTimeKind kind)
        {
            return new DateTime(1970, 1, 1, 0, 0, 0, kind).AddSeconds(epochValue);
        }

		private static int os_date (LuaState L) {
		  CharPtr s = luaL_optstring(L, 1, "%c");
          lua_Number? usetime = luaL_opt<lua_Number?>(L, nullable_checknumber, 2, null);
          DateTime stm;

		  if (s[0] == '!') {  /* UTC? */
              stm = (usetime == null) ? DateTime.UtcNow : EpochToDateTime(usetime.Value, DateTimeKind.Utc);
			s.inc();  /* skip `!' */
		  }
		  else
              stm = (usetime == null) ? DateTime.Now : EpochToDateTime(usetime.Value, DateTimeKind.Local);
          if (strcmp(s, "*t") == 0) {
			lua_createtable(L, 0, 9);  /* 9 = number of fields */
			setfield(L, "sec", stm.Second);
			setfield(L, "min", stm.Minute);
			setfield(L, "hour", stm.Hour);
			setfield(L, "day", stm.Day);
			setfield(L, "month", stm.Month);
			setfield(L, "year", stm.Year);
			setfield(L, "wday", (stm.DayOfWeek - DayOfWeek.Sunday) + 1);
			setfield(L, "yday", stm.DayOfYear);
			setboolfield(L, "isdst", stm.IsDaylightSavingTime() ? 1 : 0);
		  }
		  else {
              string result = strftime(s, stm);
              lua_pushlstring(L, new CharPtr(result), (uint)result.Length);
#if false
			CharPtr cc = new char[3];
			luaL_Buffer b;
			cc[0] = '%'; cc[2] = '\0';
			luaL_buffinit(L, b);
			for (; s[0] != 0; s.inc()) {
			  if (s[0] != '%' || s[1] == '\0')  /* no conversion specifier? */
				luaL_addchar(b, s[0]);
			  else {
				uint reslen;
				CharPtr buff = new char[200];  /* should be big enough for any conversion result */
				s.inc();
				cc[1] = s[0];
				reslen = strftime(buff, buff.Length, cc, stm);
				luaL_addlstring(b, buff, reslen);
			  }
			}
			luaL_pushresult(b);
#endif // #if 0
		  }
			return 1;
		}


		private static int os_time (LuaState L) {
		  DateTime t;
		  if (lua_isnoneornil(L, 1))  /* called without args? */
			t = DateTime.UtcNow;  /* get current time */
		  else {
			luaL_checktype(L, 1, LUA_TTABLE);
			lua_settop(L, 1);  /* make sure table is at the top */
			int sec = getfield(L, "sec", 0);
			int min = getfield(L, "min", 0);
			int hour = getfield(L, "hour", 0);
			int day = getfield(L, "day", -1);
			int month = getfield(L, "month", -1);
			int year = getfield(L, "year", -1);
			bool isdst = getboolfield(L, "isdst") != 0;
            t = new DateTime(year, month, day, hour, min, sec, DateTimeKind.Utc);
		  }
		  lua_pushnumber(L, DateTimeToEpoch(t));
		  return 1;
		}


		private static int os_difftime (LuaState L) {
		  long seconds = (long)(luaL_checknumber(L, 1) - luaL_optnumber(L, 2, 0));
		  lua_pushnumber(L, seconds);
		  return 1;
		}

		/* }====================================================== */

		// locale not supported yet
		private static int os_setlocale (LuaState L) {		  
		  /*
		  static string[] cat = {LC_ALL, LC_COLLATE, LC_CTYPE, LC_MONETARY,
							  LC_NUMERIC, LC_TIME};
		  static string[] catnames[] = {"all", "collate", "ctype", "monetary",
			 "numeric", "time", null};
		  CharPtr l = luaL_optstring(L, 1, null);
		  int op = luaL_checkoption(L, 2, "all", catnames);
		  lua_pushstring(L, setlocale(cat[op], l));
		  */
		  CharPtr l = luaL_optstring(L, 1, null);
		  lua_pushstring(L, "C");
		  return (l == null || l.ToString() == "C") ? 1 : 0;
		}


		private static int os_exit (LuaState L) {
#if XBOX
			luaL_error(L, "os_exit not supported on XBox360");
#elif SILVERLIGHT
            throw new SystemException();
#else
            exit(EXIT_SUCCESS);
#endif
			return 0;
		}

		private readonly static luaL_Reg[] syslib = {
		  new luaL_Reg("clock",     os_clock),
		  new luaL_Reg("date",      os_date),
		  new luaL_Reg("difftime",  os_difftime),
		  new luaL_Reg("execute",   os_execute),
		  new luaL_Reg("exit",      os_exit),
		  new luaL_Reg("getenv",    os_getenv),
		  new luaL_Reg("remove",    os_remove),
		  new luaL_Reg("rename",    os_rename),
		  new luaL_Reg("setlocale", os_setlocale),
		  new luaL_Reg("time",      os_time),
		  new luaL_Reg("tmpname",   os_tmpname),
		  new luaL_Reg(null, null)
		};

		/* }====================================================== */



		public static int luaopen_os (LuaState L) {
		  luaL_register(L, LUA_OSLIBNAME, syslib);
		  return 1;
		}

	}
}
