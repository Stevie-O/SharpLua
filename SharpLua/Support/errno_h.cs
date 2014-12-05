using System;

using System.Collections.Generic;
using System.Text;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace SharpLua
{
    partial class Lua
    {
#if WindowsCE
        [DllImport("coredll")]
#else
        [DllImport("kernel32", CharSet=CharSet.Auto)]
#endif
        static extern int FormatMessage(int dwFlags, int source, int messageId, int languageId, StringBuilder buffer, int nSize, IntPtr Arguments);

        const int FORMAT_MESSAGE_FROM_SYSTEM = 0x1000;
        const int FORMAT_MESSAGE_IGNORE_INSERTS = 0x0200;

        static string FormatWin32Message(int win32error)
        {
            StringBuilder sb = new StringBuilder(256);
            int x = FormatMessage(FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS, 0, win32error, 0, sb, sb.Capacity, IntPtr.Zero);
            if (x > 0) return sb.ToString(0, x);
            return null;
        }

        // According to the /usr/include/errno.h I read,
        // GNU libc uses a per-thread errno
#if WindowsCE
        static ThreadLocalSlot<object> __errno = new ThreadLocalSlot<object>();
        static object _errno { get { return __errno.Value; } set { __errno.Value = value; } }
#else
        [ThreadStatic]
        static object _errno;
#endif

        public static object errno
        {
            get { return _errno; }
        }

        public static void seterrno(int error)
        {
            _errno = error;
        }

        public static void seterrno(Exception error)
        {
            _errno = error;
        }

        /// <summary>
        /// Totally not part of ANSI C
        /// </summary>
        /// <param name="error"></param>
        /// <returns></returns>
        public static int getinterror(object error)
        {
            if (error == null) return 0;
            if (error is int) return (int) error;
            if (error is Win32Exception)
                return ((Win32Exception)error).NativeErrorCode;
            if (error is ExternalException)
                return ((ExternalException)error).ErrorCode;
            if (error is Exception)
            {
                Exception ex = error as Exception;
                int hr = Marshal.GetHRForException(ex);
                int err;
                if (((hr >> 16) & 0x3FFF) == 7) // FACILITY_WIN32
                    err = hr & 0xFFFF;// Win32
                else
                    err = hr;
            }
            return -1; // No freaking clue.
        }

        public static string strerror(object error)
        {
            StringBuilder sb = new StringBuilder();
            if (error == null) return "error #???";
            if (error is Exception) return ((Exception)error).Message;
            if (error is int)
            {
                int int_error = (int)error;
                string win32msg = FormatWin32Message(int_error);
                if (win32msg != null) return win32msg;
                if (int_error >= -32767)
                    return string.Format("error #{0}", int_error);
                return string.Format("error 0x{0:x8}", (uint)int_error);
            }
            // who knows what this is
            return String.Format("error {0}", error);
        }
    }
}
