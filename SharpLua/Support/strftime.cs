using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharpLua
{
    partial class Lua
    {
        static string strftime(CharPtr fmt, DateTime time)
        {
            StringBuilder sb = new StringBuilder();
            fmt = new CharPtr(fmt);
            char ch;
            while ((ch = fmt[0]) != '\0')
            {
                fmt.inc();
                if (ch != '%') { sb.Append(ch); continue; }
                ch = fmt[0]; fmt.inc(); // note that since the last bit of a CharPtr MUST be \0, this will never go wrong
                if (ch == '\0') break;
                /*
%a	abbreviated weekday name (e.g., Wed)
%A	full weekday name (e.g., Wednesday)
%b	abbreviated month name (e.g., Sep)
%B	full month name (e.g., September)
%c	date and time (e.g., 09/16/98 23:48:10)
    Manpage says: The preferred date and time representation for the current locale.
%C     The century number (year/100) as a 2-digit integer. (SU)
%d	day of the month (16) [01-31]
%D  Equivalent to %m/%d/%y.  (Yecch—for Americans only.  Americans should note that in other countries %d/%m/%y is rather common.  This means that  in  international  context  this  format  is
    ambiguous and should not be used.) (SU)
%e     Like %d, the day of the month as a decimal number, but a leading zero is replaced by a space. (SU)
%E     Modifier: use alternative format, see below. (SU)
%F     Equivalent to %Y-%m-%d (the ISO 8601 date format). (C99)
%G     The  ISO 8601  week-based  year  (see  NOTES)  with century as a decimal number.  The 4-digit year corresponding to the ISO week number (see %V).  This has the same format and value as %Y,
        except that if the ISO week number belongs to the previous or next year, that year is used instead. (TZ)

%g     Like %G, but without century, that is, with a 2-digit year (00-99). (TZ)
%h     Equivalent to %b.  (SU)
%H	hour, using a 24-hour clock (23) [00-23]
%I	hour, using a 12-hour clock (11) [01-12]
%j     The day of the year as a decimal number (range 001 to 366).
%k     The hour (24-hour clock) as a decimal number (range 0 to 23); single digits are preceded by a blank.  (See also %H.)  (TZ)
%l     The hour (12-hour clock) as a decimal number (range 1 to 12); single digits are preceded by a blank.  (See also %I.)  (TZ)
%M	minute (48) [00-59]
%m	month (09) [01-12]
%n     A newline character. (SU)
%O     Modifier: use alternative format, see below. (SU)
%p     Either "AM" or "PM" according to the given time value, or the corresponding strings for the current locale.  Noon is treated as "PM" and midnight as "AM".
%P     Like %p but in lowercase: "am" or "pm" or a corresponding string for the current locale. (GNU)
%r     The time in a.m. or p.m. notation.  In the POSIX locale this is equivalent to %I:%M:%S %p.  (SU)
%R     The time in 24-hour notation (%H:%M).  (SU) For a version including the seconds, see %T below.
%s     The number of seconds since the Epoch, 1970-01-01 00:00:00 +0000 (UTC). (TZ)
%S	second (10) [00-61]
%t     A tab character. (SU)
%T     The time in 24-hour notation (%H:%M:%S).  (SU)
%u     The day of the week as a decimal, range 1 to 7, Monday being 1.  See also %w.  (SU)
%U     The week number of the current year as a decimal number, range 00 to 53, starting with the first Sunday as the first day of week 01.  See also %V and %W.
%V     The ISO 8601 week number (see NOTES) of the current year as a decimal number, range 01 to 53, where week 1 is the first week that has at least 4 days in the new year.  See also %U and  %W.
        (SU)
%w	weekday (3) [0-6 = Sunday-Saturday]
%W     The week number of the current year as a decimal number, range 00 to 53, starting with the first Monday as the first day of week 01.
%x     The preferred date representation for the current locale without the time.
%X     The preferred time representation for the current locale without the date.
%Y	full year (1998)
%y	two-digit year (98) [00-99]
%%	the character `%´
%z     The +hhmm or -hhmm numeric timezone (that is, the hour and minute offset from UTC). (SU)
%Z     The timezone name or abbreviation.
                */

                // (SMO) I have no idea how the week stuff works (%G, %g, %U, %V, %W) work so I'm leaving 
                // them to future implementors.
                switch (ch)
                {
                    case 'a': sb.Append(time.ToString("ddd")); break;
                    case 'A': sb.Append(time.ToString("dddd")); break;
                    case 'h':
                    case 'b': sb.Append(time.ToString("MMM")); break;
                    case 'B': sb.Append(time.ToString("MMMM")); break;
                    case 'c': sb.Append(time.ToString()); break;
                    case 'C': sb.Append((time.Year / 100).ToString("00")); break;
                    case 'd': sb.Append(time.ToString("dd")); break;
                    case 'D': sb.Append(time.ToString("MM/dd/yy")); break;
                    case 'e': sb.AppendFormat("{0,2}", time.ToString("%d")); break;
                    case 'F': sb.Append(time.ToString("yyyy-MM-dd")); break;
                    case 'H': sb.Append(time.ToString("HH")); break;
                    case 'I': sb.Append(time.ToString("hh")); break;
                    case 'j': sb.Append(time.DayOfYear.ToString("000")); break;
                    case 'k': sb.AppendFormat("{0,2}", time.ToString("%H")); break;
                    case 'l': sb.AppendFormat("{0,2}", time.ToString("%h")); break;
                    case 'M': sb.Append(time.ToString("mm")); break;
                    case 'm': sb.Append(time.ToString("MM")); break;
                    case 'n': sb.Append('\n'); break;
                    case 'p': sb.Append(time.ToString("tt")); break;
                    case 'P': sb.Append(time.ToString("tt").ToLower()); break;
                    case 'r': sb.Append(time.ToString("T")); break;
                    case 'R': sb.Append(time.ToString("HH:mm")); break;
                    case 's': sb.Append(DateTimeToEpoch(time).ToString("F0")); break;
                    case 'S': sb.Append(time.ToString("ss")); break;
                    case 't': sb.Append('\t'); break;
                    case 'T': sb.Append(time.ToString("HH:mm:ss")); break;
                    case 'u': 
                    case 'w': int dow_tmp = time.DayOfWeek - DayOfWeek.Sunday; 
                              if (dow_tmp == 0 && ch == 'u') dow_tmp = 7;
                              sb.Append(dow_tmp.ToString());
                              break;
                    case 'x': sb.Append(time.ToString("d")); break;
                    case 'X': sb.Append(time.ToString("T")); break;
                    case 'Y': sb.Append(time.ToString("yyyy")); break;
                    case 'y': sb.Append(time.ToString("yy")); break;
                    case 'z': string ztmp = time.ToString("zzz");
                              if (ztmp.IndexOf(':') >= 0) ztmp = ztmp.Replace(":", "");
                              sb.Append(ztmp);
                              break;
                    case 'Z': // No easy way to implement htis under .NET!
                              break;
                    case '%': sb.Append('%'); break;
                    // anything else is ignored
                } // switch
            } // while
            return sb.ToString();
        }
    }
}
