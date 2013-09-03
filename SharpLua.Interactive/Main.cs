using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SharpLua;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;

namespace SharpLua.Interactive
{
    public class Program
    {
        /// <summary>
        /// The Prompt used in Interactive Mode
        /// </summary>
        public static string Prompt
        { get; set; }

        static void die(string message)
        {
            die(message, 1);
        }
        static void die(string message, int exitCode)
        {
            Console.Error.WriteLine("Error: {0}", message);
            Environment.Exit(exitCode);
        }

        enum InteractiveOption
        {
            Auto,
            Yes,
            No,
        }

        /// <summary>
        /// A REPL (Read, Eval, Print, Loop function) for #Lua
        /// </summary>
        public static void Main()
        {
            if (Debugger.IsAttached)
            {
                // if a debugger is attached, ONLY catch LuaException
                // This way, we can track bugs in other parts of the code.
                try
                {
                    RealMain();
                }
                catch (LuaException luaex)
                {
                    die(luaex.Message);
                }
            }
            else
            {
                try
                {
                    RealMain();
                }
                catch (Exception ex)
                {
                    if (ex is LuaException)
                        die(ex.Message);
                    else
                        die(ex.ToString(), 127);
                }
            }
        }

        static void RealMain()
        {
            // TODO: Better arg parsing/checking, make it more like the C lua

            InteractiveOption GoInteractive = InteractiveOption.Auto;

            // Create global variables
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            //Stopwatch sw = new Stopwatch();
            //sw.Start();
            
            //sw.Stop();
            //Console.WriteLine(sw.ElapsedMilliseconds);

#if DEBUG
#if true
            LuaRuntime.RegisterModule(typeof(TestModule));
            LuaRuntime.RegisterModule(typeof(TestModule2));
#endif

            // how to handle errors
            //LuaRuntime.SetVariable("DEBUG", true);
            LuaRuntime.SetVariable("DEBUG", false); 
            // We don't need the C# traceback.
            // All it is is [error]
            //     at LuaInterface.ThrowErrorFromException
            //     [...]
            // Which we don't need
            
#else
            LuaRuntime.SetVariable("DEBUG", false);
#endif

            Prompt = "> ";
            
            // This gets the real, 100%, full command line, including argv[0]
            string[] args = Environment.GetCommandLineArgs();
            bool did_e = false;
            int argi;
            for (argi = 1; argi < args.Length; argi++)
            {
                string arg = args[argi];
                if (arg[0] != '-') break;   // first non-option parameter stops things
                if (arg.Length == 1) 
                { 
                    // bare '-' == use stdin in non-interactive mode
                    GoInteractive = InteractiveOption.No;
                    break;
                }
                if (arg[1] == '-') { argi++; break; } // "--" ends option parameters
                switch (arg[1]) {
                    case 'i':
                        GoInteractive = InteractiveOption.Yes;
                        break;
                    case 'N':
                    case 'n':
                        GoInteractive = InteractiveOption.No;
                        break;
                    case 'l':
                        string lib;
                        if (arg.Length == 2)
                        {
                            ++argi;
                            if (argi >= args.Length) die("Missing parameter after -l");
                            lib = args[argi];
                        }
                        else
                        {
                            lib = arg.Substring(2);
                        }
                        LuaRuntime.Require(lib);
                        break;
                    case 'e':
                        did_e = true;
                        string expr;
                        if (arg.Length == 2)
                        {
                            ++argi;
                            if (argi >= args.Length) die("Missing parameter after -e");
                            expr = args[argi];
                        }
                        else
                        {
                            expr = arg.Substring(2);
                        }
                        LuaRuntime.Run(expr);
                        break;
                    case 'v':
                        LuaRuntime.PrintBanner();
                        Environment.Exit(0);
                        break;
                    default:
                        die("Undefined option: " + arg);
                        break;
                } // switch 
            } // for

            // if there's any parameters left, they must be a script filename
            if (argi < args.Length)
            {
                did_e = true;
                // the following is now sorted out by RunFile
                /*
                LuaTable t = LuaRuntime.GetLua().NewTable("arg");
                for (int i3 = 0; i3 < args.Length; i3++)
                    t[i3 - argi] = args[i3];
                t["n"] = args.Length - argi;
                */
                if (File.Exists(args[argi]))
                    LuaRuntime.SetVariable("_WORKDIR", Path.GetDirectoryName(args[argi]));
                else
                    LuaRuntime.SetVariable("_WORKDIR", Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath));
                LuaRuntime.RunFile(args[argi], args, argi);
            }

            if (GoInteractive == InteractiveOption.Yes || 
                (GoInteractive == InteractiveOption.Auto && !did_e)
                )
            {
                if (!did_e)
                    LuaRuntime.PrintBanner();
                LuaRuntime.SetVariable("_WORKDIR", Path.GetDirectoryName(typeof(Program).Assembly.Location));
                while (true)
                {
                    Console.Write(Prompt);
                    string line = Console.ReadLine();

                    if (line == "quit" || line == "exit" || line == "bye" || line == null)
                    {
                        break;
                    }
                    else
                    {
                        try
                        {
                            object[] v = LuaRuntime.GetLua().DoString(line, "<stdin>");
                            if (v == null || v.Length == 0)
#if DEBUG
                                Console.WriteLine("=> [no returned value]");
#else
                                ;
#endif
                            else
                            {
                                Console.Write("=> ");
                                for (int i = 0; i < v.Length; i++)
                                    if (v[i] == null)
                                        Console.WriteLine("<nil>");
                                    else
                                        Console.Write(v[i].ToString() + (i != v.Length - 1 ? ", " : ""));
                                Console.WriteLine();
                            }
                        }
                        catch (LuaSourceException ex)
                        {
                            for (int i = 1; i < ex.Column; i++)
                                Console.Write(" ");

                            // Offset for prompt
                            for (int i = 0; i < Prompt.Length; i++)
                            {
                                //Console.WriteLine(i);
                                Console.Write(" ");
                            }

                            Console.WriteLine("^");
                            Console.WriteLine(ex.GenerateMessage("<stdin>"));
                        }
                        catch (Exception error)
                        {
                            object dbg = LuaRuntime.GetVariable("DEBUG");

                            if (dbg != null && (dbg is bool && (bool)dbg == true))
                                Console.WriteLine(error.ToString());
                            else
                                Console.WriteLine(error.Message);
                            LuaRuntime.SetVariable("LASTERROR", error);
                        }
                    }
                }
            }
        }
    }

#if true
    // Lua Test Modules
    // Used primarily for testing the LuaModule and LuaFunction attributes.
    // Feel free to expand or delete them.

    [LuaModule("TestModule")]
    class TestModule
    {
        [LuaFunction("test")]
        public static void PrintHi()
        {
            Console.WriteLine("hi");
        }
        
        public object b(object self)
        {
            return self;
        }
        
        public void a(int t, string s, double x, LuaTable tbl, object o, bool b)
        {
            //Console.WriteLine(self);
            Console.WriteLine(t==null);
            Console.WriteLine(s==null);
            Console.WriteLine(x==null);
            Console.WriteLine(tbl==null);
            Console.WriteLine(o==null);
            Console.WriteLine(b==null);
        }

    }

    [LuaModule]
    class TestModule2
    {
        [LuaFunction]
        public static void PrintHi()
        {
            Console.WriteLine("hi");
        }
    }
#endif
}
