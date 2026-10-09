using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace KiCadDrc
{
    // ------------------------------------------------------------------ helpers

    static class S
    {
        public static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        public static string F(double x) { return x.ToString("0.####", Inv); }
        public static string Mm(double x) { return F(x) + " mm"; }
        public static string Oz(double x) { return F(x) + " oz"; }
        public static double R(double x) { return Math.Round(x, 3); }
        public static double CeilTo(double x, double step) { return R(Math.Ceiling(x / step - 1e-9) * step); }
        public static double D(object o) { return Convert.ToDouble(o, Inv); }

        public static JavaScriptSerializer Json()
        {
            return new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        }

        public static void Write(string path, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text, new UTF8Encoding(false));
        }

        public static string Html(string s)
        {
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }

        public static System.Text.RegularExpressions.Match Match(string input, string pattern)
        {
            return System.Text.RegularExpressions.Regex.Match(input, pattern);
        }
    }

    // Runs a command line tool with a real timeout (stdout and stderr are both read asynchronously)
    static class Proc
    {
        public class Result { public int ExitCode; public string Output; public bool TimedOut; }

        public static Result Run(string exe, string args, int timeoutMs)
        {
            var info = new System.Diagnostics.ProcessStartInfo(exe, args)
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using (var p = System.Diagnostics.Process.Start(info))
            {
                var stdout = p.StandardOutput.ReadToEndAsync();
                var stderr = p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit(timeoutMs))
                {
                    try { p.Kill(); } catch (Exception) { }
                    return new Result { TimedOut = true, ExitCode = -1, Output = "" };
                }
                p.WaitForExit();
                return new Result { ExitCode = p.ExitCode, Output = (stdout.Result + stderr.Result).Trim() };
            }
        }
    }

    // Indented JSON that KiCad reads (JavaScriptSerializer writes a single line)
    static class JsonWriter
    {
        public static string Write(object o) { var sb = new StringBuilder(); Value(sb, o, 0); sb.Append('\n'); return sb.ToString(); }

        static void Value(StringBuilder sb, object o, int indent)
        {
            if (o == null) { sb.Append("null"); return; }
            if (o is string) { sb.Append(Quote((string)o)); return; }
            if (o is bool) { sb.Append((bool)o ? "true" : "false"); return; }
            if (o is int || o is long) { sb.Append(Convert.ToString(o, S.Inv)); return; }
            // "R": shortest round-trip form (no precision loss for small/large values)
            if (o is double) { var d = (double)o; sb.Append(d == Math.Floor(d) && Math.Abs(d) < 1e15 ? d.ToString("0.0", S.Inv) : d.ToString("R", S.Inv)); return; }
            // Numbers read from an existing .kicad_pro come back as decimal; written back with their original precision (e.g. 1.0, 0.25)
            if (o is decimal) { sb.Append(((decimal)o).ToString(S.Inv)); return; }
            if (o is float) { sb.Append(((float)o).ToString("R", S.Inv)); return; }
            var map = o as IDictionary;
            if (map != null)
            {
                if (map.Count == 0) { sb.Append("{}"); return; }
                sb.Append("{\n"); int i = 0;
                foreach (DictionaryEntry e in map)
                {
                    sb.Append(' ', (indent + 1) * 2).Append(Quote((string)e.Key)).Append(": ");
                    Value(sb, e.Value, indent + 1);
                    sb.Append(++i < map.Count ? ",\n" : "\n");
                }
                sb.Append(' ', indent * 2).Append('}'); return;
            }
            var list = o as IList;
            if (list != null)
            {
                if (list.Count == 0) { sb.Append("[]"); return; }
                sb.Append("[\n");
                for (int i = 0; i < list.Count; i++)
                {
                    sb.Append(' ', (indent + 1) * 2); Value(sb, list[i], indent + 1);
                    sb.Append(i < list.Count - 1 ? ",\n" : "\n");
                }
                sb.Append(' ', indent * 2).Append(']'); return;
            }
            throw new InvalidOperationException("JSON: " + o.GetType());
        }

        static string Quote(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in s)
            {
                if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                else if (c < 0x20) sb.AppendFormat("\\u{0:x4}", (int)c);
                else sb.Append(c);
            }
            return sb.Append('"').ToString();
        }
    }

    // Dictionary that keeps insertion order (for JSON output order)
    class OrderedMap : System.Collections.Specialized.OrderedDictionary
    {
        public OrderedMap Put(string k, object v) { this[k] = v; return this; }
    }
}
