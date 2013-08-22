/*
 * User: edfrederickson
 * Date: 10/8/2012
 * Time: 6:37 PM
 * Copyright 2012 LoDC
 */
using System;

namespace SharpLua
{
    /// <summary>
    /// string extensions
    /// </summary>
    public static class StringExt
    {
        public static string Repeat(this string s, int n)
        {
            string s2 = "";
            for (int i = 0; i < n; i++)
                s2 += s;
            return s2;
        }

        public static bool IsNullOrWhiteSpace(string s)
        {
            if (s == null) return true;
            if (s.Trim().Length == 0) return true;
            return false;
        }

    }
}
