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
    // ------------------------------------------------------------------ command line (tests and automation)

    static class Cli
    {
        public static int Run(KiCadEnvironment env, List<IManufacturer> manufacturers, string[] args)
        {
            var a = new Dictionary<string, string>();
            for (int i = 0; i < args.Length; i++)
                if (args[i].StartsWith("--")) a[args[i].Substring(2)] = i + 1 < args.Length && !args[i + 1].StartsWith("--") ? args[++i] : "";
            // --default resolves every inconsistency anyway, so no warnings are printed for it
            if (!a.ContainsKey("default"))
                foreach (var w in env.Warnings) Console.WriteLine(Tx.CliWarning + w);

            if (a.ContainsKey("help"))
            {
                Console.WriteLine("KiCad DRC.exe [--lang en|tr] [--maker JLC] [--layers N] [--thickness X] [--outer OZ] [--inner OZ] [--stackup NAME]\n" +
                    "              [--mask green] [--finish enig] [--edge vcut|routed] [--margin 0.05]\n" +
                    "              (--show | --apply | --verify [--keep] | --default | --screenshot file.png [--tab N] [--wait] [--notify])\n" +
                    "              --project X.kicad_pro (--analyze | --update | --bom [--import-codes file] [--build [--boards N]])\n" +
                    Tx.CliHelpFooter);
                return 0;
            }
            if (a.ContainsKey("default")) { env.RestoreDefaults(); Console.WriteLine(Tx.CliRestored); return 0; }

            string code = a.ContainsKey("maker") ? a["maker"] : "JLC";
            var m = manufacturers.FirstOrDefault(x => x.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
            if (m == null) throw new InvalidOperationException(Tx.UnknownMaker(code, string.Join(", ", manufacturers.Select(x => x.Code))));
            // Every option that is not given comes from the manufacturer's recommendation for the options above it
            Func<string, double> number = name =>
            {
                if (!a.ContainsKey(name)) return double.NaN;
                double x;
                if (!double.TryParse(a[name], NumberStyles.Float, S.Inv, out x)) throw new InvalidOperationException(Tx.MustBeNumber(name, a[name]));
                return x;
            };
            var s = m.Recommended(a.ContainsKey("layers") ? (int)number("layers") : 2, number("thickness"), number("outer"), number("inner"));
            if (a.ContainsKey("margin")) s.Margin = Math.Min(0.15, Math.Max(0, number("margin")));
            if (a.ContainsKey("stackup")) s.Stackup = a["stackup"];
            if (a.ContainsKey("mask")) s.Mask = a["mask"].ToLowerInvariant();
            if (a.ContainsKey("finish")) s.Finish = a["finish"];
            if (a.ContainsKey("edge")) s.Edge = a["edge"];

            if (a.ContainsKey("screenshot"))
            {
                Program.SetProcessDPIAware();
                Application.EnableVisualStyles();
                using (var f = new MainForm(env, manufacturers, a.ContainsKey("layers") ? s : null))
                {
                    f.StartPosition = FormStartPosition.Manual; f.Location = new Point(-4000, -4000);
                    f.Show();
                    if (a.ContainsKey("tab")) f.SelectTab(int.Parse(a["tab"], S.Inv));
                    if (a.ContainsKey("notify")) f.ShowBanner(Tx.AppliedReopen("DRC_JLC_4L"), Color.FromArgb(220, 252, 231), Color.FromArgb(21, 94, 52), null);
                    var clock = System.Diagnostics.Stopwatch.StartNew();
                    int duration = a.ContainsKey("wait") ? 5000 : 600;   // the production tab reads the BOM in the background
                    while (clock.ElapsedMilliseconds < duration) { Application.DoEvents(); Thread.Sleep(10); }
                    using (var bmp = new Bitmap(f.Width, f.Height)) { f.DrawToBitmap(bmp, new Rectangle(0, 0, f.Width, f.Height)); bmp.Save(a["screenshot"]); }
                }
                return 0;
            }

            var r = m.Calculate(s);

            // Existing project: --project path.kicad_pro (--analyze | --update | --bom)
            if (a.ContainsKey("project"))
            {
                var p = ProjectUpdater.Analyze(a["project"]);
                if (a.ContainsKey("bom"))
                {
                    var bom = Production.Bom(env, p);
                    if (a.ContainsKey("import-codes"))
                    {
                        var conflicts = new List<string>();
                        int filled = Production.ImportCodes(a["import-codes"], bom, conflicts);
                        Console.WriteLine(Tx.CliImported(filled) + (conflicts.Count > 0 ? " | " + Tx.CliConflicts + string.Join("; ", conflicts) : ""));
                        Production.SaveCodes(p, bom);
                    }
                    foreach (var x in bom)
                        Console.WriteLine("{0,-18} {1,-5} {2,-22} {3,-30} {4}", x.Category, x.Qty, x.Value, x.Package, Production.IsValidCode(x.Lcsc) ? x.Lcsc : Tx.CliMissing);
                    Console.WriteLine(Tx.CliBomSummary(bom.Count, bom.Sum(x => x.Qty), bom.Count(x => !Production.IsValidCode(x.Lcsc))));
                    if (a.ContainsKey("build"))
                    {
                        var notes = new List<string>();
                        foreach (var file in Production.Build(env, p, m, s, r, bom, a.ContainsKey("boards") ? (int)number("boards") : 5, notes))
                            Console.WriteLine(Tx.CliCreated + file);
                        foreach (var n in notes) Console.WriteLine(Tx.CliNote + n);
                    }
                    return 0;
                }
                var plan = ProjectUpdater.Plan(p, s.Layers);
                Console.WriteLine(Tx.CliProjectLine(p.Name, p.Layers, s.Layers, r.Stackup.Name));
                Console.WriteLine(Tx.CliItems + string.Join(", ", p.Items.OrderBy(x => x.Key).Select(x => x.Key + "=" + x.Value)));
                Console.WriteLine(Tx.CliInnerUsage + (p.InnerUsage.Count == 0 ? Tx.None : string.Join(", ", p.InnerUsage.OrderBy(x => x.Key).Select(x => "In" + x.Key + ".Cu=" + x.Value))));
                if (p.Locks.Count > 0) Console.WriteLine(Tx.CliLock + string.Join(", ", p.Locks));
                string note; var ps = ProjectUpdater.SelectionFromProject(m, p, s, out note);
                if (ps != null) Console.WriteLine(Tx.CliSelectionFromProject(ps.Layers, S.Mm(ps.Thickness), S.Oz(ps.Outer) + "/" + S.Oz(ps.Inner), ps.Stackup, ps.Mask, ps.Finish) + (note != null ? "  (" + note + ")" : ""));
                else Console.WriteLine(Tx.CliNoSelection + note);
                if (plan.Blocker != null) Console.WriteLine(Tx.CliBlocked + plan.Blocker);
                if (!a.ContainsKey("update")) return plan.Blocker == null ? 0 : 3;
                string error, error2;
                var before = Verifier.CountDrc(env, p.PcbPath, out error);
                var result = ProjectUpdater.Update(env, m, s, r, p);
                var after = Verifier.CountDrc(env, p.PcbPath, out error2);
                foreach (var d in result.Changes) Console.WriteLine("  • " + d);
                string drcNote = error ?? error2;
                Console.WriteLine(Tx.DrcBeforeAfter(Verifier.DrcText(before), Verifier.DrcText(after)) + (drcNote != null ? "  (" + drcNote + ")" : ""));
                Console.WriteLine(Tx.CliBackup + result.Backup);
                return 0;
            }

            foreach (var row in r.Rows) Console.WriteLine("{0,-20} {1,-28} {2,-22} {3}", row.Group, row.Name, row.Value, row.Source);
            foreach (var n in r.Notes) Console.WriteLine(Tx.CliNote + n);
            if (a.ContainsKey("apply")) Console.WriteLine(Tx.CliApplied + env.Apply(m, s, r));
            if (a.ContainsKey("verify"))
            {
                // PASS / FAIL / ERROR tokens stay English so test scripts can parse them in both languages
                var v = Verifier.Run(env, s, r, a.ContainsKey("keep"));
                foreach (var g in v.Passed) Console.WriteLine("  PASS   " + g);
                foreach (var g in v.Failed) Console.WriteLine("  FAIL   " + g);
                if (v.Error != null) Console.WriteLine("  ERROR  " + v.Error);
                return v.Success ? 0 : 2;
            }
            return 0;
        }
    }
}
