using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace KiCadDrc
{
    // Placement data (CPL) the way JLC reads it. Reference: the Fabrication Toolkit plugin that JLC's own KiCad guide recommends
    // (github.com/bennymeg/Fabrication-Toolkit, Apache-2.0), compared file by file on 2026-09-27 (production_outputs.md):
    //   - SMD parts at the footprint origin, other parts (through-hole, unspecified) at the centre of their pads' bounding box;
    //   - bottom rotation 180 - angle (Production.JlcRotation);
    //   - a rotation correction per footprint name for packages whose zero orientation differs between KiCad's library and
    //     JLC's (table below, same rows, order and matching rules as the plugin's transformations.csv).
    // Deliberate differences:
    //   - footprints from EasyEDA / LCSC (easyeda2kicad, JLC's own library) are already drawn in JLC's orientation, so they
    //     get no correction. The plugin matches them by name only and turns e.g. an EasyEDA LQFP by 270°;
    //   - polarised capacitors (the plugin's CP_EIA / CP_Elec / C_Elec rows, +180°) are not turned. Their JLC orientation
    //     depends on the LCSC part, not on the package: EasyEDA names them "-FD" (forward, + on the left like KiCad's
    //     CP_Elec, checked on C46550415 "CAP-SMD_BD6.3-L6.6-W6.6-LS7.3-FD": pad 1 / + at the left) or "-RD" (reversed).
    //     A blanket 180° reverses every forward part, so they are reported for the preview check instead (2026-10-08).
    class BoardFootprint
    {
        public string Ref, Id;            // Id = "library:footprint"
        public bool Smd;
        public double X, Y, Angle;        // footprint origin and orientation (board coordinates, Y down)
        public double CentreX, CentreY;   // centre of the pads' bounding box; the origin when there are no pads
        public int SmdPads, ThtPads;      // solder joints: SMD pads and plated through-hole pads (JLC charges per joint)
    }

    static class JlcPlacement
    {
        // Fabrication Toolkit transformations.csv: regex on the footprint name, rotation added after the bottom mirroring.
        // A regex containing ':' is matched against "library:name". First match wins (the duplicate rows are kept as in the source).
        static readonly Tuple<string, double>[] Corrections =
        {
            T("^Bosch_LGA-", 90), T("^DFN-", 270), T("^DFN-", 270),
            T("^D_SOT-23", 180), T("^HTSSOP-", 270), T("^HTSSOP-", 270), T("^HTSSOP-", 270), T("^JST_GH_SM", 180), T("^JST_PH_S", 180),
            T("^LQFP-", 270), T("^MSOP-", 270), T("^PowerPAK_SO-8_Single", 270), T("^QFN-", 90), T("^R_Array_Concave_", 90),
            T("^R_Array_Convex_", 90), T("^SC-74-6", 180), T("^SOIC-", 270), T("^SOIC-16_", 270), T("^SOIC-8_", 270),
            T("^SOIC127P798X216-8N", -90), T("^SOP-(?!18_)", 270), T("^SOP-(?!18_)", 270), T("^SOP-18_", 0), T("^SOP-18_", 0),
            T("^SOP-4_", 0), T("^SOP-4_", 0), T("^SOT-143", 180), T("^SOT-223", 180), T("^SOT-23", 180), T("^SOT-353", 180),
            T("^SOT-363", 180), T("^SOT-89", 180), T("^SSOP-", 270), T("^SW_SPST_B3", 90), T("^TDSON-8-1", 270), T("^TO-277", 90),
            T("^TQFP-", 270), T("^TSOT-23", 180), T("^TSSOP-", 270), T("^UDFN-10", 270), T("^USON-10", 270), T("^VSON-8_", 270),
            T("^VSSOP-10_-", 270), T("^VSSOP-10_-", 270), T("^VSSOP-8_", 180), T("^VSSOP-8_", 270), T("^VSSOP-8_3.0x3.0mm_P0.65mm", 270),
            T("^qfn-", 90),
        };
        static Tuple<string, double> T(string regex, double rotation) { return Tuple.Create(regex, rotation); }

        // Polarised capacitors of KiCad's library: not turned (see above), listed for the check in JLC's preview
        public static bool IsPolarisedCapacitor(string id)
        {
            if (string.IsNullOrEmpty(id) || IsLcscFootprint(id)) return false;
            int colon = id.IndexOf(':');
            return Regex.IsMatch(colon >= 0 ? id.Substring(colon + 1) : id, "^(CP_EIA-|CP_Elec_|C_Elec_|CP_Radial|CP_Axial|CP_Tantalum)");
        }

        // EasyEDA / LCSC footprint: its library says so, or its name uses EasyEDA's size notation ("_L10.0-W10.0", "-L6.6-W6.6")
        public static bool IsLcscFootprint(string id)
        {
            id = id ?? "";
            int colon = id.IndexOf(':');
            string library = colon >= 0 ? id.Substring(0, colon) : "", name = colon >= 0 ? id.Substring(colon + 1) : id;
            return Regex.IsMatch(library, "easyeda|lcsc|jlc", RegexOptions.IgnoreCase)
                || Regex.IsMatch(name, @"[_-]L\d+(\.\d+)?-W\d+(\.\d+)?(-|$)");
        }

        // Rotation JLC needs on top of KiCad's for this footprint ("library:name"); 0 for EasyEDA / LCSC footprints
        public static double Correction(string id)
        {
            if (string.IsNullOrEmpty(id) || IsLcscFootprint(id)) return 0;
            int colon = id.IndexOf(':');
            string library = colon >= 0 ? id.Substring(0, colon) : "", name = colon >= 0 ? id.Substring(colon + 1) : id;
            foreach (var c in Corrections)
                if (Regex.IsMatch(c.Item1.Contains(':') ? id : name, c.Item1)) return c.Item2;
            // like the plugin: no match on the name, then the library nickname is tried
            if (library.Length > 0)
                foreach (var c in Corrections)
                    if (Regex.IsMatch(library, c.Item1)) return c.Item2;
            return 0;
        }

        static readonly Regex AtRe = new Regex(@"\(at\s+(-?[\d.]+)\s+(-?[\d.]+)(?:\s+(-?[\d.]+))?\)");
        static readonly Regex SizeRe = new Regex(@"\(size\s+(-?[\d.]+)\s+(-?[\d.]+)\)");

        static double N(string s) { return double.Parse(s, CultureInfo.InvariantCulture); }

        // Footprints of a .kicad_pcb with origin, orientation and pad centre. Pad positions in the file are relative to the
        // footprint and not rotated; a pad's angle in the file includes the footprint's. KiCad rotation (Y down, positive =
        // counter-clockwise on screen): x' = x cos a + y sin a, y' = -x sin a + y cos a.
        public static Dictionary<string, BoardFootprint> Read(string pcb)
        {
            var result = new Dictionary<string, BoardFootprint>();
            var root = Sexp.Root(pcb, "kicad_pcb");
            foreach (var node in Sexp.Children(pcb, root).Where(c => c.Name == "footprint"))
            {
                string text = node.Text(pcb);
                var parts = Sexp.Children(pcb, node);
                var at = parts.FirstOrDefault(c => c.Name == "at");
                var refMatch = Regex.Match(text, @"\(property\s+""Reference""\s+""((?:[^""\\]|\\.)*)""");
                if (at == null || !refMatch.Success) continue;
                var a = AtRe.Match(at.Text(pcb));
                var f = new BoardFootprint
                {
                    Ref = refMatch.Groups[1].Value, Id = Regex.Match(text, @"^\(footprint\s+""((?:[^""\\]|\\.)*)""").Groups[1].Value,
                    X = N(a.Groups[1].Value), Y = N(a.Groups[2].Value), Angle = a.Groups[3].Success ? N(a.Groups[3].Value) : 0
                };
                var attr = parts.FirstOrDefault(c => c.Name == "attr");
                f.Smd = attr != null && Regex.IsMatch(attr.Text(pcb), @"\bsmd\b");

                // bounding box of the pads in the footprint's own (unrotated) frame, then its centre rotated onto the board
                double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
                foreach (var pad in parts.Where(c => c.Name == "pad"))
                {
                    string p = pad.Text(pcb);
                    var kind = Regex.Match(p, @"^\(pad\s+""[^""]*""\s+(\w+)").Groups[1].Value;
                    if (kind == "smd") f.SmdPads++; else if (kind == "thru_hole") f.ThtPads++;
                    var pa = AtRe.Match(p); var ps = SizeRe.Match(p);
                    if (!pa.Success || !ps.Success) continue;
                    double px = N(pa.Groups[1].Value), py = N(pa.Groups[2].Value), w = N(ps.Groups[1].Value), h = N(ps.Groups[2].Value);
                    double rel = ((pa.Groups[3].Success ? N(pa.Groups[3].Value) : 0) - f.Angle) * Math.PI / 180;
                    bool circle = Regex.IsMatch(p, @"^\(pad\s+""[^""]*""\s+\w+\s+circle\b");
                    double hx = circle ? w / 2 : Math.Abs(w / 2 * Math.Cos(rel)) + Math.Abs(h / 2 * Math.Sin(rel));
                    double hy = circle ? w / 2 : Math.Abs(w / 2 * Math.Sin(rel)) + Math.Abs(h / 2 * Math.Cos(rel));
                    x0 = Math.Min(x0, px - hx); x1 = Math.Max(x1, px + hx); y0 = Math.Min(y0, py - hy); y1 = Math.Max(y1, py + hy);
                }
                double cx = 0, cy = 0;
                if (x0 <= x1) { cx = (x0 + x1) / 2; cy = (y0 + y1) / 2; }
                double r = f.Angle * Math.PI / 180;
                f.CentreX = Math.Round(f.X + cx * Math.Cos(r) + cy * Math.Sin(r), 6);
                f.CentreY = Math.Round(f.Y - cx * Math.Sin(r) + cy * Math.Cos(r), 6);
                if (!result.ContainsKey(f.Ref)) result[f.Ref] = f;   // duplicate references (unannotated) are not placed anyway
            }
            return result;
        }

        // Applies position (non-SMD: pad centre) and rotation corrections to the CPL rows; returns "REF +180°" texts of the
        // corrected rotations so the user can check exactly those parts in JLC's preview. polarised receives the references of
        // polarised capacitors with KiCad footprints (not turned; their + must be checked in the preview).
        public static List<string> Adjust(List<CplRow> cpl, Dictionary<string, BoardFootprint> board, List<string> polarised)
        {
            var corrected = new List<string>();
            foreach (var row in cpl)
            {
                BoardFootprint f;
                if (!board.TryGetValue(row.Ref, out f)) continue;
                row.Footprint = f.Id;
                if (IsPolarisedCapacitor(f.Id)) polarised.Add(row.Ref);
                if (!f.Smd) { row.X = Math.Round(f.CentreX, 4); row.Y = Math.Round(-f.CentreY, 4); }   // CPL Y is up
                double c = Correction(f.Id);
                if (c != 0)
                {
                    row.Rotation = Production.JlcRotation(row.Rotation + c, false);   // only normalises
                    corrected.Add(row.Ref + " " + (c > 0 ? "+" : "") + S.F(c) + "°");
                }
            }
            return corrected;
        }
    }
}
