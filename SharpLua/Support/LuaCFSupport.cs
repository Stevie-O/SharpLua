using System;

using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.IO;
using System.Diagnostics;

namespace SharpLua
{
    partial class Lua
    {
#if WindowsCE
        static string _fake_current_directory;
        /// <summary>
        /// Fake current directory (used to emulate fopen())
        /// </summary>
        public static string FakeCurrentDirectory { get { return _fake_current_directory; } set { _fake_current_directory = value; } }

        [DllImport("coredll")]
        static extern void TerminateProcess(IntPtr hProcess, int exitCode);

        static IntPtr GetCurrentProcess()
        {
            const int SYS_HANDLE_BASE = 64;
            const int SH_CURPROC = 2;
            return (IntPtr)(SH_CURPROC + SYS_HANDLE_BASE);
        }

        class ConsoleStream : Stream
        {
            int fdno;

            [DllImport("coredll")]
            static extern IntPtr _getstdfilex(int fdno);

            [DllImport("coredll")]
            static unsafe extern int fwrite(byte* buffer, int cbElem, int nElem, IntPtr file);

            [DllImport("coredll")]
            static unsafe extern int fread(byte* buffer, int cbElem, int nElem, IntPtr file);

            [DllImport("coredll")]
            static extern int fseek(IntPtr file, int offset, int whence);

            [DllImport("coredll")]
            static extern int fgetc(IntPtr file);

            [DllImport("coredll")]
            static extern int fflush(IntPtr file);

            IntPtr Handle { get { return _getstdfilex(fdno); } }

            public ConsoleStream(int fdno)
            {
                if (fdno < 0 || fdno > 2) throw new ArgumentOutOfRangeException("fdno");
                this.fdno = fdno;
            }

            public override bool CanSeek { get { return fseek(Handle, 0, 1) >= 0; } }
            public override bool CanRead { get { return fdno == 0; } }
            public override bool CanWrite { get { return fdno > 0; } }

            public override unsafe int Read(byte[] buffer, int offset, int count)
            {
                if (fdno > 0) throw new NotSupportedException();
                int result;
                fixed (byte* pbuffer = &buffer[offset])
                {
                    result = fread(pbuffer, 1, count, Handle);
                }
                if (result >= 0) return result;
                throw new IOException("fread() failure");
            }

            public override unsafe void Write(byte[] buffer, int offset, int count)
            {
                if (fdno == 0) throw new NotSupportedException();
                int result;
                fixed (byte* pbuffer = &buffer[offset])
                {
                    result = fwrite(pbuffer, 1, count, Handle);
                }
                if (result >= 0) return;
                throw new IOException("fread() failure");
            }

            public override int ReadByte()
            {
                return fgetc(Handle);
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                if (offset < 0 || offset > int.MaxValue)
                    throw new ArgumentOutOfRangeException("offset");
                int result = fseek(Handle, (int)offset, (int)origin);
                if (result < 0) throw new Exception("fseek() failed");
                return result;
            }

            public override void Flush()
            {
                fflush(Handle);
            }

            public override long Length
            {
                get { throw new NotSupportedException(); }
            }

            public override long Position
            {
                get
                {
                    return Seek(0, SeekOrigin.Current);
                }
                set
                {
                    Seek(value, SeekOrigin.Begin);
                }
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }
        }
#endif
    }
}
