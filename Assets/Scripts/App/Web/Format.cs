using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Margin.App.Web
{
    /// <summary>Dates as the web keeps them: "yyyy-MM-dd" keys and "yyyy-MM-ddTHH:mm" stamps (core.js D).</summary>
    public static class D
    {
        public static string Key(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        public static DateTime Parse(string k)
        {
            if (string.IsNullOrEmpty(k) || k.Length < 10) return DateTime.MinValue;
            return DateTime.TryParseExact(k.Substring(0, 10), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : DateTime.MinValue;
        }

        public static DateTime Date(DateTime d) => d.Date;
        public static DateTime Add(DateTime d, int days) => d.AddDays(days);
        public static bool Same(DateTime a, DateTime b) => a.Date == b.Date;
        public static string Stamp(DateTime d) => d.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);

        public static DateTime ParseStamp(string s)
        {
            var d = Parse(s);
            if (s != null && s.Length >= 16 && int.TryParse(s.Substring(11, 2), out var h) && int.TryParse(s.Substring(14, 2), out var m))
                d = d.AddHours(h).AddMinutes(m);
            return d;
        }
    }

    /// <summary>Number and date formatting of ui.js (F). English, no CultureInfo lookups (IL2CPP friendly).</summary>
    public static class F
    {
        public static readonly string[] MES = { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };

        /// <summary>1234.4 -> "1,234".</summary>
        public static string N(double x)
        {
            long n = (long)Math.Round(x, MidpointRounding.AwayFromZero);
            string digits = Math.Abs(n).ToString(CultureInfo.InvariantCulture);
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < digits.Length; i++)
            {
                if (i > 0 && (digits.Length - i) % 3 == 0) sb.Append(',');
                sb.Append(digits[i]);
            }
            return n < 0 ? "-" + sb : sb.ToString();
        }

        /// <summary>12500 -> "12.5k", 150000 -> "150k".</summary>
        public static string K(double x)
        {
            if (x < 10000) return N(x);
            var s = (x / 1000).ToString(x >= 100000 ? "0" : "0.0", CultureInfo.InvariantCulture).Replace(".0", "");
            return s + "k";
        }

        public static string Date(DateTime d) => $"{MES[d.Month - 1]} {d.Day}, {d.Year}";
        public static string Date(string key) => Date(D.Parse(key));
        public static string DateShort(DateTime d) => $"{MES[d.Month - 1]} {d.Day}";
        public static string DateShort(string key) => DateShort(D.Parse(key));

        /// <summary>"now", "5 min", "3 h", "2 d", or "Nov 3".</summary>
        public static string Ago(DateTime stamp, DateTime now)
        {
            int min = (int)Math.Round((now - stamp).TotalMinutes);
            if (min < 1) return "now";
            if (min < 60) return $"{min} min";
            int hrs = (int)Math.Round(min / 60.0);
            if (hrs < 24) return $"{hrs} h";
            int days = (int)Math.Round(hrs / 24.0);
            if (days < 7) return $"{days} d";
            return DateShort(stamp);
        }

        public static string Ago(string stamp, DateTime now) => Ago(D.ParseStamp(stamp), now);

        public static string Pages(int n) => n == 1 ? "1 page" : $"{N(n)} pages";
        public static string Books(int n) => n == 1 ? "1 book" : $"{N(n)} books";
        public static string Pct(double x) => $"{Math.Round(x * 100, MidpointRounding.AwayFromZero)}%";
    }

    /// <summary>core.js Fmt (port of the old UI/Fmt.cs).</summary>
    public static class Fmt
    {
        public static readonly string[] Days = { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };
        public static readonly string[] Months = { "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };

        /// <summary>"Wednesday, November 11".</summary>
        public static string DateLong(DateTime d) => $"{Days[(int)d.DayOfWeek]}, {Months[d.Month - 1]} {d.Day}";
        public static string Time(int h, int m) => $"{h:00}:{m:00}";
        public static string Pages(int n) => F.Pages(n);
        public static string Plural(int n, string one, string many) => n == 1 ? $"1 {one}" : $"{F.N(n)} {many}";

        /// <summary>"Monday to Friday", "every day", "on Mondays, Wednesdays and Saturdays".</summary>
        public static string DayList(IEnumerable<int> days)
        {
            var set = days.Distinct().OrderBy(d => (d + 6) % 7).ToList();
            if (set.Count == 7) return "every day";
            if (set.Count == 5 && !set.Contains(0) && !set.Contains(6)) return "Monday to Friday";
            if (set.Count == 2 && set.Contains(0) && set.Contains(6)) return "on weekends";
            var names = set.Select(d => Days[d] + "s").ToList();
            if (names.Count == 0) return "";
            if (names.Count == 1) return "on " + names[0];
            return "on " + string.Join(", ", names.Take(names.Count - 1)) + " and " + names.Last();
        }
    }
}
