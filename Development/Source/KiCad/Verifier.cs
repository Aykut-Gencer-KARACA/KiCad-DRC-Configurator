using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace KiCadDrc
{
    // ------------------------------------------------------------------ verification with KiCad
    // Runs the generated settings in kicad-cli with a test board that contains deliberately faulty items:
    // the files must open without errors, every rule must report its violation, and compliant items must report nothing.

    class Verification
    {
        public List<string> Passed = new List<string>(), Failed = new List<string>();
        public string Error;
        public bool Success { get { return Error == null && Failed.Count == 0; } }
        public string Summary()
        {
            if (Error != null) return Error;
            return Tx.VerifySummary(Passed.Count + Failed.Count, Passed.Count) +
                (Failed.Count > 0 ? "\n\n" + Tx.VerifyFailedList + "\n•  " + string.Join("\n•  ", Failed) : "");
        }
    }

    static class Verifier
    {
        // Violation types that must not appear on the "clean" items
        static readonly string[] RuleViolations = { "track_width", "clearance", "hole_clearance", "drill_out_of_range", "annular_width",
            "assertion_failure", "copper_edge_clearance", "hole_to_hole", "items_not_allowed", "connection_width" };

        class Check { public string Label, Expected, Tag; }

        public static Verification Run(KiCadEnvironment env, Selection s, RuleSet r) { return Run(env, s, r, false); }

        // keep: do not delete the test board (for the developer, to open it in KiCad)
        public static Verification Run(KiCadEnvironment env, Selection s, RuleSet r, bool keep)
        {
            var v = new Verification();
            string cli = Path.Combine(env.InstallDir, @"bin\kicad-cli.exe");
            if (!File.Exists(cli)) { v.Error = Tx.KiCadCliNotFoundVerify(cli); return v; }

            var checks = new List<Check>();
            var items = new StringBuilder();
            int counter = 100;
            Func<string> U = () => string.Format("00000000-0000-0000-0000-{0:D12}", counter++);
            // expected: a rule name (from the same string table used to write the rules) or a violation type; "|" separates alternatives
            Action<string, string, string> check = (label, expected, tag) => checks.Add(new Check { Label = label, Expected = expected, Tag = tag });
            Func<string, string, string> pad = (def, net) =>
                "\t\t(pad " + def + " (layers \"*.Cu\" \"*.Mask\")" + (net == null ? "" : " (net \"" + net + "\")") + " (uuid \"" + U() + "\"))\n";
            Action<string, int, string> fp = (name, x, pads) => items.Append(
                "\t(footprint \"test:" + name + "\" (layer \"F.Cu\") (uuid \"" + U() + "\") (at " + x + " 25)\n" +
                "\t\t(property \"Reference\" \"" + name + "\" (at 0 -5 0) (layer \"F.Fab\") (uuid \"" + U() + "\") (effects (font (size 1 1) (thickness 0.15))))\n" +
                pads + "\t)\n");
            Func<double, string> f = S.F;

            // Items that break each rule -> expected violation
            items.Append("\t(segment (start 5 50) (end 25 50) (width " + f(S.R(r.Limits["track"] - 0.03)) + ") (layer \"F.Cu\") (net \"T1\") (uuid \"" + U() + "\"))\n");
            check(Tx.BoardRuleName("min_track_width"), "Track width", "[T1]");
            items.Append("\t(segment (start 40 0.15) (end 60 0.15) (width 0.25) (layer \"F.Cu\") (net \"T2\") (uuid \"" + U() + "\"))\n");
            check(Tx.BoardRuleName("min_copper_edge_clearance"), "edge clearance", "[T2]");
            string npth = f(S.R(r.Limits["npth_min"] - 0.1));
            fp("T3", 10, pad("\"\" np_thru_hole circle (at 0 0) (size " + npth + " " + npth + ") (drill " + npth + ")", null));
            check(Tx.CheckNpthMin, Tx.Rule("npth_hole"), "of T3");
            fp("T4", 25, pad("\"1\" thru_hole circle (at 0 0) (size 8 8) (drill " + f(S.R(r.Limits["max_drill"] + 0.5)) + ")", "T4"));
            check(Tx.CheckMaxDrill, Tx.Rule("pth_hole"), "of T4");
            string ringPad = f(S.R(0.8 + 2 * (r.Limits["ring"] - 0.04)));
            fp("T5", 40, pad("\"1\" thru_hole circle (at 0 0) (size " + ringPad + " " + ringPad + ") (drill 0.8)", "T5"));
            check(Tx.Rule("pth_ring"), Tx.Rule("pth_ring"), "of T5");
            double slot = S.R(r.Limits["slot_min"] - 0.05);
            fp("T6", 52, pad("\"1\" thru_hole oval (at 0 0) (size " + f(slot + 0.8) + " 3.2) (drill oval " + f(slot) + " 2.4)", "T6"));
            check(Tx.Rule("plated_slot"), Tx.Rule("plated_slot"), "of T6");
            string nslot = f(S.R(r.Limits["npth_slot_min"] - 0.2));
            fp("T7", 64, pad("\"\" np_thru_hole oval (at 0 0) (size " + nslot + " 2.4) (drill oval " + nslot + " 2.4)", null));
            check(Tx.Rule("npth_slot"), Tx.Rule("npth_slot"), "of T7");
            fp("T8", 76, "\t\t(pad \"1\" smd rect (at 0 0) (size 0.2 0.2) (layers \"F.Cu\" \"F.Mask\") (net \"T8\") (uuid \"" + U() + "\"))\n");
            check(Tx.Rule("min_smd_pad"), Tx.Rule("min_smd_pad"), "of T8");
            fp("T9", 88, pad("\"1\" thru_hole circle (at 0 0) (size 0.6 0.6) (drill 0.3)", "T9"));
            check(Tx.Rule("pth_min_warning"), Tx.Rule("pth_min_warning"), "of T9");
            if (s.Layers >= 4)
            {
                items.Append("\t(via blind (at 128 25) (size 0.6) (drill 0.3) (layers \"F.Cu\" \"In1.Cu\") (net \"T10\") (uuid \"" + U() + "\"))\n");
                check(Tx.CheckBlindVia, Tx.Rule("no_blind_vias"), "[T10]");
            }
            if (s.Layers >= 6)
            {
                items.Append("\t(via (at 138 25) (size 1.4) (drill 0.8) (layers \"F.Cu\" \"B.Cu\") (net \"T11\") (uuid \"" + U() + "\"))\n");
                check(Tx.Rule("via_in_pad"), Tx.Rule("via_in_pad"), "[T11]");
            }
            // Castellated pads: small hole and two holes close to each other (same net: only the hole rules trigger)
            string castHole = f(S.R(r.Limits["cast_min"] - 0.1));
            fp("T14", 20, pad("\"1\" thru_hole circle (at 0 12) (size 1.2 1.2) (drill " + castHole + ") (property pad_prop_castellated)", "T14"));
            check(Tx.CheckCastellatedMin, Tx.Rule("castellated_hole"), "of T14");
            double castGap = S.R(0.8 + r.Limits["cast_min"] - 0.1);
            fp("T15", 30, pad("\"1\" thru_hole circle (at 0 12) (size 1.2 1.2) (drill 0.8) (property pad_prop_castellated)", "T15") +
                pad("\"2\" thru_hole circle (at " + f(castGap) + " 12) (size 1.2 1.2) (drill 0.8) (property pad_prop_castellated)", "T15"));
            check(Tx.Rule("castellated_spacing"), Tx.Rule("castellated_spacing"), "of T15");
            // Copper clad NPTH pad: ring 0.2 (0.45 recommended)
            fp("T16", 45, pad("\"\" np_thru_hole circle (at 0 12) (size 1.0 1.0) (drill 0.6)", null));
            check(Tx.Rule("npth_ring"), Tx.Rule("npth_ring"), "of T16");
            // Two vias (same net): 0.05 below the hole-to-hole limit
            double viaGap = S.R(r.ViaDrill + r.Limits["via_h2h"] - 0.05);
            items.Append("\t(via (at 60 37) (size " + f(r.ViaDiameter) + ") (drill " + f(r.ViaDrill) + ") (layers \"F.Cu\" \"B.Cu\") (net \"T17\") (uuid \"" + U() + "\"))\n");
            items.Append("\t(via (at " + f(60 + viaGap) + " 37) (size " + f(r.ViaDiameter) + ") (drill " + f(r.ViaDrill) + ") (layers \"F.Cu\" \"B.Cu\") (net \"T17\") (uuid \"" + U() + "\"))\n");
            check(Tx.Rule("via_hole_to_hole"), Tx.Rule("via_hole_to_hole"), "[T17]");
            // Via hole next to a track of another net. KiCad applies the hole-track rule only where the via has no pad on that
            // layer (on padded layers copper clearance applies and via ring + copper clearance already exceed JLC's 0.2). So the
            // test uses an inner layer track next to a via whose unused pads are removed (4+ layers).
            if (s.Layers >= 4)
            {
                double trackY = S.R(37 + r.ViaDrill / 2 + (r.Limits["via_hole_track"] - 0.02) + 0.125);
                items.Append("\t(via (at 75 37) (size " + f(r.ViaDiameter) + ") (drill " + f(r.ViaDrill) + ") (layers \"F.Cu\" \"B.Cu\") (remove_unused_layers yes) (net \"T18\") (uuid \"" + U() + "\"))\n");
                items.Append("\t(segment (start 72 " + f(trackY) + ") (end 78 " + f(trackY) + ") (width 0.25) (layer \"In1.Cu\") (net \"T18b\") (uuid \"" + U() + "\"))\n");
                // With margin 0 this limit equals the net class clearance; KiCad then reports the pair once, as "clearance"
                check(Tx.CheckViaHoleTrackInner, Tx.Rule("via_hole_track") + "|hole_clearance|clearance", "[T18]");
            }
            // Two PTH pads (same net): 0.1 below the Board Setup hole-to-hole limit
            double padGap = S.R(1.0 + r.Limits["pad_h2h"] - 0.1);
            string padSize = f(S.R(1.0 + 2 * (r.Limits["ring"] + 0.05)));
            fp("T19", 90, pad("\"1\" thru_hole circle (at 0 12) (size " + padSize + " " + padSize + ") (drill 1.0)", "T19") +
                pad("\"2\" thru_hole circle (at " + f(padGap) + " 12) (size " + padSize + " " + padSize + ") (drill 1.0)", "T19"));
            check(Tx.BoardRuleName("min_hole_to_hole"), "hole_to_hole", "of T19");

            // Compliant items -> no violation at all (false alarm check)
            string cleanPad = f(S.R(1.0 + 2 * (r.Limits["ring"] + 0.1)));
            fp("T12", 100, pad("\"1\" thru_hole circle (at 0 0) (size " + cleanPad + " " + cleanPad + ") (drill 1.0)", "T12a") +
                "\t\t(pad \"2\" smd rect (at 4 0) (size 1 1.5) (layers \"F.Cu\" \"F.Mask\") (net \"T12b\") (uuid \"" + U() + "\"))\n" +
                pad("\"\" np_thru_hole circle (at 8 0) (size 3.2 3.2) (drill 3.2)", null));
            check(Tx.CheckNoFalseAlarm, null, "of T12");
            // The net class default via and every preset via must comply
            int vx = 108;
            foreach (var p in r.ViaPresets)
                items.Append("\t(via (at " + (vx += 3) + " 40) (size " + f(p[0]) + ") (drill " + f(p[1]) + ") (layers \"F.Cu\" \"B.Cu\") (net \"T13\") (uuid \"" + U() + "\"))\n");
            check(Tx.CheckPresetVias(string.Join(", ", r.ViaPresets.Select(p => f(p[0]) + "/" + f(p[1])))), null, "[T13]");

            string pcb = Files.Pcb(s, r).Replace("\t(embedded_fonts no)\n",
                "\t(gr_rect (start 0 0) (end 150 60) (stroke (width 0.05) (type default)) (fill no) (layer \"Edge.Cuts\") (uuid \"" + U() + "\"))\n" +
                items + "\t(embedded_fonts no)\n");

            string folder = Path.Combine(env.DataDir, "test-" + Guid.NewGuid().ToString("N"));
            try
            {
                S.Write(Path.Combine(folder, "t.kicad_pro"), Files.Pro(r, "t.kicad_pro"));
                S.Write(Path.Combine(folder, "t.kicad_dru"), Files.Dru(r));
                S.Write(Path.Combine(folder, "t.kicad_pcb"), pcb);
                string report = Path.Combine(folder, "r.json");
                var run = Proc.Run(cli, "pcb drc --format json -o \"" + report + "\" \"" + Path.Combine(folder, "t.kicad_pcb") + "\"", 120000);
                if (run.TimedOut) { v.Error = Tx.KiCadCliTimeout; return v; }
                if (!File.Exists(report)) { v.Error = Tx.KiCadCouldNotOpenGenerated(run.Output); return v; }

                var root = (IDictionary<string, object>)S.Json().DeserializeObject(File.ReadAllText(report, Encoding.UTF8));
                var violations = ((object[])root["violations"]).Cast<IDictionary<string, object>>().Select(x => new
                {
                    Type = (string)x["type"],
                    Text = (string)x["description"],
                    Items = string.Join(" | ", ((object[])x["items"]).Cast<IDictionary<string, object>>().Select(o => (string)o["description"]))
                }).ToList();

                foreach (var c in checks)
                {
                    // Expected: the rule name appearing in the violation text or the violation type (e.g. hole_to_hole)
                    var expected = c.Expected == null ? null : c.Expected.Split('|');
                    bool passed = expected != null
                        ? violations.Any(x => x.Items.Contains(c.Tag) &&
                            expected.Any(b => x.Type == b || x.Text.IndexOf(b, StringComparison.OrdinalIgnoreCase) >= 0))
                        : !violations.Any(x => RuleViolations.Contains(x.Type) && x.Items.Contains(c.Tag));
                    (passed ? v.Passed : v.Failed).Add(c.Label);
                }
                return v;
            }
            catch (Exception e) { v.Error = Tx.VerifyCouldNotRun(e.Message); return v; }
            finally { if (!keep) { try { Directory.Delete(folder, true); } catch (Exception) { } } else Console.WriteLine("  Test board: " + folder); }
        }

        // Counts a project's KiCad DRC result: {errors, warnings, unconnected}. Does not write into the project
        // (the report goes to the data folder). Returns null and an error text when it cannot run.
        public static int[] CountDrc(KiCadEnvironment env, string pcbPath, out string error)
        {
            error = null;
            string cli = Path.Combine(env.InstallDir, @"bin\kicad-cli.exe");
            if (!File.Exists(cli)) { error = Tx.KiCadCliNotFound(cli); return null; }
            string report = Path.Combine(env.DataDir, "drc-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                Directory.CreateDirectory(env.DataDir);
                var run = Proc.Run(cli, "pcb drc --format json -o \"" + report + "\" \"" + pcbPath + "\"", 600000);
                if (run.TimedOut) { error = Tx.KiCadCliTimeout; return null; }
                if (!File.Exists(report)) { error = Tx.KiCadCouldNotOpenProject(run.Output); return null; }
                var root = (IDictionary<string, object>)S.Json().DeserializeObject(File.ReadAllText(report, Encoding.UTF8));
                var violations = ((object[])root["violations"]).Cast<IDictionary<string, object>>().ToList();
                int unconnected = root.ContainsKey("unconnected_items") && root["unconnected_items"] is object[] ? ((object[])root["unconnected_items"]).Length : 0;
                return new[] { violations.Count(x => (string)x["severity"] == "error"), violations.Count(x => (string)x["severity"] == "warning"), unconnected };
            }
            catch (Exception e) { error = e.Message; return null; }
            finally { try { File.Delete(report); } catch (Exception) { } }
        }

        public static string DrcText(int[] d) { return d == null ? "?" : Tx.DrcCounts(d[0], d[1], d[2]); }
    }
}
