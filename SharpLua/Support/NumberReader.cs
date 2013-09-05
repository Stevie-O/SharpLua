using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

using LUA_NUMBER = System.Double;

namespace SharpLua
{
    partial class Lua
    {
        /// <summary>
        /// Class used to simulate/emulate fscanf(f, "%lf", ...)
        /// </summary>
        class NumberReader
        {
            readonly Stream s;
            LUA_NUMBER result;
            readonly StringBuilder sb = new StringBuilder(10); // a reasonable default, I'd say

            bool neg = false;

            int inchar_skipws()
            {
                int ch;
                do
                {
                    ch = fgetc(s);
                } while (ch >= 0 && char.IsWhiteSpace((char)ch));
                return ch;
            }

            int inchar()
            {
                int ch = fgetc(s);
                return ch;
            }

            /// <summary>
            /// Attempts to read a number from 's' with the same semantics as
            /// fscanf(s, "%lf", &n)
            /// </summary>
            /// <param name="s">Stream to read from</param>
            /// <param name="n">Where to store </param>
            /// <returns>1 if a value was successfully read
            /// 0 if no value was successfully read
            /// -1 if no value was successfuly read *and* we hit EOF</returns>
            public static int ReadNumber(Stream s, out LUA_NUMBER n)
            {
                if (!s.CanRead) { n = default(LUA_NUMBER); return -1; }
                NumberReader nr = new NumberReader(s);
                int answer = nr.ReadNumber();
                n = nr.result;
                return answer;
            }
            /// <summary>
            /// Constructs a NumberReader object that will attempt to read 's' with the same semantics as 
            /// fscanf(s, "%lf", &foo)
            /// </summary>
            /// <param name="s"></param>
            private NumberReader(Stream s)
            {
                this.s = s;
            }

            int ReadNumber()
            {
                return ReadNumberImpl();
            }

            /// <summary>
            /// Reads NAN and returns 1 (if it was actually NAN) or 0 (if it wasn't, like NAx)
            /// </summary>
            /// <returns></returns>
            int ReadNAN()
            {
                int ch = inchar();
                if (ch < 0 || char.ToLower((char)ch) != 'a') return 0;
                ch = inchar();
                if (ch < 0 || char.ToLower((char)ch) != 'n') return 0;
                result = double.NaN;
                return 1;
            }

            /// <summary>
            /// Reads INF and returns 1 (if it was actually INF) or 0 (if it wasn't, like INx)
            /// </summary>
            /// <returns></returns>
            int ReadINF()
            {
                int ch = inchar();
                if (ch < 0 || char.ToLower((char)ch) != 'n') return 0;
                ch = inchar();
                if (ch < 0 || char.ToLower((char)ch) != 'f') return 0;
                ch = inchar();
                // this bit is tricky: it might be INF or it might be spelled out as INFINITY
                if (ch >= 0)
                {
                    if (char.ToLower((char)ch) == 'i')
                    {
                        ch = inchar();
                        if (ch < 0 || char.ToLower((char)ch) != 'n') return 0;
                        ch = inchar();
                        if (ch < 0 || char.ToLower((char)ch) != 'i') return 0;
                        ch = inchar();
                        if (ch < 0 || char.ToLower((char)ch) != 't') return 0;
                        ch = inchar();
                        if (ch < 0 || char.ToLower((char)ch) != 'y') return 0;
                    }
                    else
                        ungetc(ch, s);
                }
                result = neg ? double.NegativeInfinity : double.PositiveInfinity;
                return 1;
            }

            static bool isdigit(int ch) { return (ch >= '0' && ch < '9'); }

            static bool isdigit(char ch) { return (ch >= '0' && ch < '9'); }

            static bool isxdigit(int ch) { return (ch >= '0' && ch < '9') || (ch >= 'A' && ch <= 'F') || (ch >= 'a' && ch <= 'f'); }

            static bool isxdigit(char ch) { return (ch >= '0' && ch < '9') || (ch >= 'A' && ch <= 'F') || (ch >= 'a' && ch <= 'f'); }

            static double getdigitvalue(char ch)
            {
                if (ch >= '0' && ch < '9') return ch - '0';
                if (ch >= 'A' && ch <= 'Z') return ch - ('A' - 10);
                if (ch >= 'a' && ch <= 'z') return ch - ('a' - 10);
                return double.NaN;
            }

            /// <summary>
            /// Given that ch is '0'..'9', returns ch.
            /// </summary>
            /// <param name="ch"></param>
            /// <returns></returns>
            static int getintvalue(char ch) { return (int)(ch & 0x0F); }

            enum ReadState
            {
                /// <summary>
                /// Expecting: digit or '.'
                /// </summary>
                FirstDigit,
                /// <summary>
                /// Expecting: digit, '.', or 'E'/'e'
                /// </summary>
                IntPart,
                /// <summary>
                /// Expecting digit or 'E'/'e'
                /// </summary>
                FracPart,
                /// <summary>
                /// Expecting digit or '+' or '-'
                /// </summary>
                ExpFirstDigit,
                /// <summary>
                /// Expecting digit
                /// </summary>
                Exponent,
            }

            int ReadNumberImpl()
            {
                int ch1 = inchar_skipws();
                if (ch1 < 0) return -1;
                char ch = (char)ch1;

                // read a possible sign
                if (ch == '+' || ch == '-')
                {
                    neg = (ch == '-');
                    ch1 = inchar();
                    if (ch1 < 0) return 0;
                    ch = (char)ch1;
                }

                // handle NAN and INF
                if (char.ToLower(ch) == 'n')
                    return ReadNAN();

                if (char.ToLower(ch) == 'i')
                    return ReadINF();

                // okay, at this point, I am expecting one of the following:
                // decimal point ('.') or digit ('0'-'9')
                if (!(ch == '.' || isdigit(ch)))
                {
                    // wait, this character is no good!
                    // We must pretend we never actually read it...
                    ungetc(ch, s);
                    return 0;
                }

                bool hex = false;
                bool any_digits = false;
                ReadState state = ReadState.FirstDigit;
                double radix = 10.0;
                double value = 0.0;
                double frac_multiplier = 0.1;
                int exp = 0;
                bool expneg = false;

                if (ch == '0')
                {
                    any_digits = true;
                    state = ReadState.IntPart; // consider it a valid read at this point
                    // Well, this won't have any effect on the *value*, although that changes our expectation of the rest
                    // of the string...
                    ch1 = inchar();
                    ch = (char)ch1;
                    if (tolower(ch) == 'x')
                    {
                        frac_multiplier = 1f / 16;
                        // activate hexadecimal magic
                        hex = true;
                        radix = 16.0;
                        ch1 = inchar(); ch = (char)ch1;
                        any_digits = false;
                        state = ReadState.FirstDigit;
                    }
                }

                for (; ch1 >= 0; ch1 = inchar(), ch = (char)ch1)
                {
                    if (state == ReadState.FirstDigit)
                    {
                        if (ch == '.') { state = ReadState.FracPart; continue; }
                        if (isdigit(ch) || (hex && isxdigit(ch)))
                        {
                            state = ReadState.IntPart;
                            any_digits = true;
                            value = value * radix + getdigitvalue(ch);
                            continue;
                        }
                        break; // out of for
                    }
                    else if (state == ReadState.IntPart)
                    {
                        if (ch == '.') { state = ReadState.FracPart; continue; }
                        if (isdigit(ch) || (hex && isxdigit(ch)))
                        {
                            any_digits = true;
                            value = value * radix + getdigitvalue(ch);
                            continue;
                        }
                        // note that the following will NEVER trigger if 'hex' is set, because the 'isxdigit' check
                        // above will eat it
                        if (tolower(ch) == 'e') { state = ReadState.ExpFirstDigit; continue; }
                        break;
                    }
                    else if (state == ReadState.FracPart)
                    {
                        if (isdigit(ch) || (hex && isxdigit(ch)))
                        {
                            any_digits = true;
                            value = value + frac_multiplier * getdigitvalue(ch);
                            frac_multiplier /= radix;
                            continue;
                        }
                        // note that the following will NEVER trigger if 'hex' is set, because the 'isxdigit' check
                        // above will eat it
                        if (tolower(ch) == 'e') { state = ReadState.ExpFirstDigit; continue; }
                        break;
                    }
                    else if (state == ReadState.ExpFirstDigit)
                    {
                        if (ch == '+' || ch == '-')
                        {
                            expneg = (ch == '-');
                            state = ReadState.Exponent;
                            continue;
                        }
                        if (isdigit(ch))
                        {
                            state = ReadState.Exponent;
                            exp = getintvalue(ch);
                            continue;
                        }
                        break;
                    }
                    else /* if (state == ReadState.Exponent) */
                    {
                        if (isdigit(ch))
                        {
                            exp = exp * 10 + getintvalue(ch);
                            continue;
                        }
                        break;
                    }
                } // for
                // un-get the last character we read, assuming there was one
                // (this will be ignored if the last "character" we read was EOF)
                ungetc(ch1, s);
                if (!any_digits) return 0; // uhh, no good, man
                if (exp != 0)
                {
                    if (expneg) exp = -exp;
                    value *= Math.Pow(10f, exp);
                }
                if (neg) value = -value;
                result = value;
                return 1; // success!
            } // ReadNumberImpl
        } // class NumberReader
    } // (partial) class Lua
}
