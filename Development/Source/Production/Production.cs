using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace KiCadDrc
{
    static class Production
    {
        public static bool IsValidCode(string k) { return k != null && Regex.IsMatch(k, @"^C\d{3,}$"); }
        public static string CodeFile(ProjectInfo p) { return Path.Combine(p.Dir, p.Name + ".lcsc.json"); }
        public static string SchematicPath(ProjectInfo p) { return Path.Combine(p.Dir, p.Name + ".kicad_sch"); }
        public static string OutputDir(ProjectInfo p) { return Path.Combine(p.Dir, DiskNames.ProductionFolder); }

        // Output file names: <project>_Rev<revision>_<content>, e.g. BLDCdriver_RevC_Gerber.zip (no revision: BLDCdriver_Gerber.zip)
        public static string FileBase(ProjectInfo p)
        {
            string rev = Regex.Replace(p.Revision ?? "", @"^rev[\s_-]*", "", RegexOptions.IgnoreCase);
            rev = Regex.Replace(rev, @"[^A-Za-z0-9.\-]", "");
            return p.Name + (rev.Length > 0 ? "_Rev" + rev : "");
        }
        public static string GerberFile(ProjectInfo p) { return FileBase(p) + "_Gerber.zip"; }
        public static string BomFile(ProjectInfo p) { return FileBase(p) + "_BOM_JLC.csv"; }
        public static string CplFile(ProjectInfo p) { return FileBase(p) + "_CPL_JLC.csv"; }
        public static string XlsxFile(ProjectInfo p) { return FileBase(p) + "_Production.xlsx"; }
        public static string NetlistFile(ProjectInfo p) { return FileBase(p) + "_Netlist.ipc"; }

        // BOM footprint like the Fabrication Toolkit: "R_0805_2012Metric" / "C_0603_1608Metric" -> "0805" / "0603", others unchanged
        public static string BomFootprint(string package)
        {
            return Regex.Replace(package ?? "", @"^(\w*_SMD:)?\w{1,4}_(\d+)_\d+Metric.*$", "$2");
        }

        static string KiCadCli(KiCadEnvironment env, string args)
        {
            string cli = Path.Combine(env.InstallDir, @"bin\kicad-cli.exe");
            if (!File.Exists(cli)) throw new InvalidOperationException(Tx.KiCadCliNotFound(cli));
            var r = Proc.Run(cli, args, 600000);
            if (r.TimedOut) throw new InvalidOperationException(Tx.KiCadCliTimeout);
            if (r.ExitCode != 0) throw new InvalidOperationException(Tx.KiCadCliFailed(r.ExitCode, r.Output));
            return r.Output;
        }

        // RFC 4180 CSV (quoted fields, "" escape)
        public static List<string[]> Csv(string text)
        {
            var rows = new List<string[]>(); var fields = new List<string>(); var sb = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (quoted)
                {
                    if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { sb.Append('"'); i++; }
                    else if (c == '"') quoted = false;
                    else sb.Append(c);
                }
                else if (c == '"') quoted = true;
                else if (c == ',') { fields.Add(sb.ToString()); sb.Clear(); }
                else if (c == '\n' || c == '\r')
                {
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                    fields.Add(sb.ToString()); sb.Clear();
                    if (fields.Count > 1 || fields[0].Length > 0) rows.Add(fields.ToArray());
                    fields.Clear();
                }
                else sb.Append(c);
            }
            if (sb.Length > 0 || fields.Count > 0) { fields.Add(sb.ToString()); rows.Add(fields.ToArray()); }
            return rows;
        }

        static string CsvField(string s) { return "\"" + (s ?? "").Replace("\"", "\"\"") + "\""; }

        public static string Category(string refs)
        {
            var m = Regex.Match(refs ?? "", @"^[A-Za-z]+");
            switch (m.Value.ToUpperInvariant())
            {
                case "C": return Tx.CatCapacitor;
                case "R": case "RV": case "VR": return Tx.CatResistor;
                case "L": return Tx.CatInductor;
                case "FB": case "BEAD": return Tx.CatFerrite;
                case "D": case "LED": return Tx.CatDiode;
                case "Q": return Tx.CatTransistor;
                case "U": case "IC": return Tx.CatIc;
                case "J": case "P": case "CN": case "CON": return Tx.CatConnector;
                case "F": return Tx.CatFuse;
                case "Y": case "X": return Tx.CatCrystal;
                case "SW": case "S": case "K": return Tx.CatSwitch;
                case "TP": return Tx.CatTestPoint;
                default: return Tx.CatOther;
            }
        }

        // Placed parts of the schematic (except DNP and "exclude from BOM"), grouped by value + footprint + LCSC code, so parts
        // with the same value but different codes stay separate rows
        public static List<BomRow> Bom(KiCadEnvironment env, ProjectInfo p)
        {
            string sch = SchematicPath(p);
            if (!File.Exists(sch)) throw new InvalidOperationException(Tx.SchematicNotFound(sch));
            string temp = Path.Combine(env.DataDir, "bom-" + Guid.NewGuid().ToString("N") + ".csv");
            try
            {
                Directory.CreateDirectory(env.DataDir);
                KiCadCli(env, "sch export bom --fields \"Reference,Value,Footprint,${QUANTITY},LCSC Part,LCSC\" --labels \"Ref,Value,Footprint,Qty,LCSC Part,LCSC\" " +
                    "--group-by \"Value,Footprint,LCSC Part,LCSC\" --ref-range-delimiter \"\" --exclude-dnp -o \"" + temp + "\" \"" + sch + "\"");
                var rows = Csv(File.ReadAllText(temp, Encoding.UTF8));
                var codes = ReadCodes(p);
                var excluded = ReadExcluded(p);
                var bom = new List<BomRow>();
                foreach (var r in rows.Skip(1).Where(r => r.Length >= 4))
                {
                    var s = new BomRow { Refs = r[0], Value = r[1], Footprint = r[2], Qty = int.Parse(r[3], S.Inv) };
                    s.Category = Category(s.Refs);
                    s.Excluded = excluded.Contains(s.Key);
                    // Priority: the schematic's "LCSC Part" (or "LCSC") field, then a code entered in the app
                    s.Lcsc = SchematicCode(r.Length > 4 ? r[4] : "", r.Length > 5 ? r[5] : "");
                    s.FromSchematic = s.Lcsc.Length > 0;
                    string saved;
                    if (!s.FromSchematic && codes.TryGetValue(s.Key, out saved)) s.Lcsc = saved;
                    bom.Add(s);
                }
                return bom;
            }
            finally { try { File.Delete(temp); } catch (Exception) { } }
        }

        // First valid code of the schematic fields ("LCSC Part", then "LCSC"); spaces and lower case are tolerated
        public static string SchematicCode(params string[] fields)
        {
            return fields.Select(x => (x ?? "").Trim().ToUpperInvariant()).FirstOrDefault(IsValidCode) ?? "";
        }

        static Dictionary<string, object> ReadCodeFile(ProjectInfo p)
        {
            try
            {
                if (!File.Exists(CodeFile(p))) return null;
                return S.Json().Deserialize<Dictionary<string, object>>(File.ReadAllText(CodeFile(p), Encoding.UTF8));
            }
            catch (Exception) { return null; }   // corrupt file: codes are entered again
        }

        // Files written before version 2.2 use Turkish key names; both are read
        static object Field(Dictionary<string, object> root, string key, string legacyKey)
        {
            if (root == null) return null;
            return root.ContainsKey(key) ? root[key] : root.ContainsKey(legacyKey) ? root[legacyKey] : null;
        }

        public static Dictionary<string, string> ReadCodes(ProjectInfo p)
        {
            var d = new Dictionary<string, string>();
            var codes = Field(ReadCodeFile(p), "codes", Legacy.CodesKey) as Dictionary<string, object>;
            if (codes != null) foreach (var e in codes) if (e.Value is string) d[e.Key] = (string)e.Value;
            return d;
        }

        public static HashSet<string> ReadExcluded(ProjectInfo p)
        {
            var h = new HashSet<string>();
            // Deserialize<T> returns arrays as ArrayList; both forms are IEnumerable
            var l = Field(ReadCodeFile(p), "do_not_place", Legacy.ExcludedKey) as IEnumerable;
            if (l != null) foreach (var x in l.OfType<string>()) h.Add(x);
            return h;
        }

        // Entered codes and "do not place" marks are written to the project folder; entries of parts no longer in the
        // schematic are kept. Codes that come from the schematic are not copied (the schematic stays the only source).
        public static void SaveCodes(ProjectInfo p, IEnumerable<BomRow> bom)
        {
            var d = ReadCodes(p);
            var h = ReadExcluded(p);
            // a stored code is dropped once the schematic provides it, unless another row without a schematic code uses the same key
            var manual = new HashSet<string>(bom.Where(x => !x.FromSchematic).Select(x => x.Key));
            foreach (var s in bom)
            {
                if (s.FromSchematic) { if (!manual.Contains(s.Key)) d.Remove(s.Key); }
                else if (IsValidCode(s.Lcsc)) d[s.Key] = s.Lcsc;
                else d.Remove(s.Key);
                if (s.Excluded) h.Add(s.Key); else h.Remove(s.Key);
            }
            var codes = new OrderedMap();
            foreach (var e in d.OrderBy(x => x.Key)) codes.Put(e.Key, e.Value);
            S.Write(CodeFile(p), JsonWriter.Write(new OrderedMap()
                .Put("description", "KiCad DRC Configurator: LCSC codes of this project (value|footprint -> code). Edit them in the app.")
                .Put("codes", codes)
                .Put("do_not_place", new ArrayList(h.OrderBy(x => x).ToList()))));
        }

        // Fills empty rows from a file with reference -> LCSC code pairs (Markdown table, CSV, text).
        // A row is filled when all its parts map to a single code; with different codes it is left alone and reported.
        public static int ImportCodes(string file, List<BomRow> bom, List<string> conflicts)
        {
            var map = new Dictionary<string, string>();
            var codeRe = new Regex(@"(?:LCSC[^C\d|]{0,12}|product-detail/)(C\d{3,})", RegexOptions.IgnoreCase);
            var refRe = new Regex(@"\b([A-Z]{1,4}\d{1,4})\b");
            foreach (var line in File.ReadAllLines(file, Encoding.UTF8))
            {
                var km = codeRe.Match(line);
                if (!km.Success) continue;
                string code = km.Groups[1].Value.ToUpperInvariant();
                // In a table row the references are in the first cell; otherwise in the text left of the code
                string refText;
                if (line.TrimStart().StartsWith("|")) refText = line.Split('|').Select(x => x.Trim()).FirstOrDefault(x => x.Length > 0) ?? "";
                else refText = line.Substring(0, km.Index);
                foreach (Match m in refRe.Matches(refText))
                    if (!map.ContainsKey(m.Groups[1].Value)) map[m.Groups[1].Value] = code;
            }
            int filled = 0;
            foreach (var s in bom.Where(x => !IsValidCode(x.Lcsc)))
            {
                var found = s.Refs.Split(',').Select(x => x.Trim()).Where(map.ContainsKey).Select(x => map[x]).Distinct().ToList();
                if (found.Count == 1) { s.Lcsc = found[0]; filled++; }
                else if (found.Count > 1) conflicts.Add(s.Refs + ": " + string.Join(" / ", found));
            }
            return filled;
        }

        public static List<CplRow> Cpl(KiCadEnvironment env, ProjectInfo p)
        {
            string temp = Path.Combine(env.DataDir, "cpl-" + Guid.NewGuid().ToString("N") + ".csv");
            try
            {
                KiCadCli(env, "pcb export pos --format csv --units mm --side both --exclude-dnp -o \"" + temp + "\" \"" + p.PcbPath + "\"");
                var rows = Csv(File.ReadAllText(temp, Encoding.UTF8));
                // Header: Ref,Val,Package,PosX,PosY,Rot,Side
                return rows.Skip(1).Where(r => r.Length >= 7).Select(r =>
                {
                    bool bottom = r[6].Trim().Equals("bottom", StringComparison.OrdinalIgnoreCase);
                    return new CplRow
                    {
                        Ref = r[0], Value = r[1] == "~" ? "" : r[1], Package = r[2],   // "~" is KiCad's empty value
                        X = double.Parse(r[3], S.Inv), Y = double.Parse(r[4], S.Inv),
                        Rotation = JlcRotation(double.Parse(r[5], S.Inv), bottom), Side = bottom ? "Bottom" : "Top"
                    };
                }).ToList();
            }
            finally { try { File.Delete(temp); } catch (Exception) { } }
        }

        // KiCad writes the footprint's own orientation for both sides. JLC reads bottom-side rotations mirrored: 180 - angle
        // (the convention of the Fabrication Toolkit that JLC's KiCad guide recommends, of KiBot's JLCPCB preset and of
        // kicad-jlcpcb-tools). Results are 0 <= angle < 360. X/Y stay KiCad's: same absolute origin as the Gerbers,
        // Y up, bottom X not negated. JLC has changed its bottom convention before: the preview must still be checked.
        public static double JlcRotation(double kicad, bool bottom)
        {
            double r = (bottom ? 180 - kicad : kicad) % 360;
            if (r < 0) r += 360;
            r = Math.Round(r, 4);
            return r >= 360 || r == 0 ? 0 : r;   // no "-0" and no 360
        }

        // Only parts JLC places go into the CPL: rows of footprints that are not in the placed BOM (fiducials, logos, board-only
        // footprints, parts with "Place" unticked) are left out. leftOut lists those that were not unticked on purpose;
        // missing lists placed BOM parts that have no footprint on the board (schematic and PCB out of sync).
        public static List<CplRow> CplForBom(List<CplRow> cpl, List<BomRow> bom, List<string> leftOut, List<string> missing)
        {
            Func<BomRow, IEnumerable<string>> refs = x => x.Refs.Split(',').Select(t => t.Trim()).Where(t => t.Length > 0);
            var placed = new HashSet<string>(bom.Where(x => !x.Excluded).SelectMany(refs));
            var excluded = new HashSet<string>(bom.Where(x => x.Excluded).SelectMany(refs));
            leftOut.AddRange(cpl.Where(x => !placed.Contains(x.Ref) && !excluded.Contains(x.Ref)).Select(x => x.Ref.Length > 0 ? x.Ref : Tx.NoReference));
            var onBoard = new HashSet<string>(cpl.Select(x => x.Ref));
            missing.AddRange(placed.Where(r => !onBoard.Contains(r)).OrderBy(r => r, StringComparer.Ordinal));
            return cpl.Where(x => placed.Contains(x.Ref)).ToList();
        }

        // Gerber + drill package with the settings of JLC's KiCad 9 guide (jlcpcb.com/help/article/how-to-generate-gerber-and-drill-files-in-kicad-9):
        //   zone fills checked before plotting (kicad-cli does not refill otherwise: a board saved with outdated fills would
        //   be plotted with the OLD copper), Protel extensions, X2 format and netlist attributes on, silkscreen clipped at
        //   the mask openings; drill: Excellon, mm, decimal, absolute origin, alternate mode for oval holes, PTH and NPTH in
        //   separate files. The board file itself is not changed by the zone check.
        static string GerberPackage(KiCadEnvironment env, ProjectInfo p, string outDir)
        {
            string temp = Path.Combine(outDir, "gerber_temp");
            if (Directory.Exists(temp)) Directory.Delete(temp, true);
            Directory.CreateDirectory(temp);
            var layers = Files.CopperNames(p.Layers).Concat(new[] { "F.Paste", "B.Paste", "F.SilkS", "B.SilkS", "F.Mask", "B.Mask", "Edge.Cuts" });
            // The trailing "\\\"" is a backslash before the closing quote, escaped for Windows argument parsing
            KiCadCli(env, "pcb export gerbers --check-zones --subtract-soldermask -l \"" + string.Join(",", layers) + "\" -o \"" + temp + "\\\\\" \"" + p.PcbPath + "\"");
            KiCadCli(env, "pcb export drill --format excellon --excellon-units mm --excellon-zeros-format decimal --drill-origin absolute " +
                "--excellon-oval-format alternate --excellon-separate-th --generate-map --map-format gerberx2 -o \"" + temp + "\\\\\" \"" + p.PcbPath + "\"");
            string zip = Path.Combine(outDir, GerberFile(p));
            if (File.Exists(zip)) File.Delete(zip);
            System.IO.Compression.ZipFile.CreateFromDirectory(temp, zip);
            Directory.Delete(temp, true);
            return zip;
        }

        // notes: warnings for the user (insufficient stock, part information not available)
        public static List<string> Build(KiCadEnvironment env, ProjectInfo p, IManufacturer m, Selection s, RuleSet r, List<BomRow> bom, int boards, List<string> notes = null)
        {
            if (boards < 1) boards = 1;
            if (notes == null) notes = new List<string>();
            var missing = bom.Where(x => !x.Excluded && !IsValidCode(x.Lcsc)).ToList();
            if (missing.Count > 0) throw new InvalidOperationException(Tx.CodesMissing(missing.Count));
            var placed = bom.Where(x => !x.Excluded).ToList();
            if (p.Layers != s.Layers) throw new InvalidOperationException(Tx.LayerMismatch(p.Layers, s.Layers));

            // Placement first: nothing is written when a placed part has no footprint on the board
            var leftOut = new List<string>(); var notOnBoard = new List<string>();
            var cpl = CplForBom(Cpl(env, p), bom, leftOut, notOnBoard);
            if (notOnBoard.Count > 0) throw new InvalidOperationException(Tx.PartsNotOnBoard(string.Join(", ", notOnBoard)));
            if (leftOut.Count > 0) notes.Add(Tx.CplLeftOut(string.Join(", ", leftOut)));
            // Through-hole parts at their pad centre, rotation corrections for KiCad library footprints (JlcPlacement)
            var polarised = new List<string>();
            var board = JlcPlacement.Read(File.ReadAllText(p.PcbPath, Encoding.UTF8));
            var corrected = JlcPlacement.Adjust(cpl, board, polarised);
            if (corrected.Count > 0) notes.Add(Tx.RotationsCorrected(string.Join(", ", corrected)));
            if (polarised.Count > 0) notes.Add(Tx.PolarisedCheck(string.Join(", ", polarised)));

            string outDir = OutputDir(p);
            Directory.CreateDirectory(outDir);
            var files = new List<string>();
            files.Add(GerberPackage(env, p, outDir));

            // BOM and CPL in the layout of the Fabrication Toolkit (the tool JLC's KiCad guide recommends): same columns,
            // designators "C1, C2", resistor/capacitor/inductor footprints as their size ("0603"), plain mm numbers, layer
            // top/bottom. Unlike the plugin the BOM carries the LCSC codes entered in the app, and parts with "Place" unticked
            // are left out. An empty value is replaced by the LCSC code.
            string bomCsv = Path.Combine(outDir, BomFile(p));
            File.WriteAllText(bomCsv, "Designator,Footprint,Quantity,Value,LCSC Part #\r\n" +
                string.Concat(placed.Select(x => CsvField(string.Join(", ", x.Refs.Split(',').Select(t => t.Trim()))) + "," + CsvField(BomFootprint(x.Package)) + "," +
                    x.Qty.ToString(S.Inv) + "," + CsvField(string.IsNullOrWhiteSpace(x.Value) || x.Value == "~" ? x.Lcsc : x.Value) + "," + CsvField(x.Lcsc) + "\r\n")), new UTF8Encoding(true));
            files.Add(bomCsv);
            string cplCsv = Path.Combine(outDir, CplFile(p));
            File.WriteAllText(cplCsv, "Designator,Mid X,Mid Y,Rotation,Layer\r\n" +
                string.Concat(cpl.Select(x => CsvField(x.Ref) + "," + S.F(x.X) + "," + S.F(x.Y) + "," + S.F(x.Rotation) + "," + x.Side.ToLowerInvariant() + "\r\n")), new UTF8Encoding(true));
            files.Add(cplCsv);
            // IPC-D-356 netlist (the plugin writes one too): lets the manufacturer compare the Gerbers with the connections
            string netlist = Path.Combine(outDir, NetlistFile(p));
            KiCadCli(env, "pcb export ipcd356 -o \"" + netlist + "\" \"" + p.PcbPath + "\"");
            files.Add(netlist);

            // Manufacturer, MPN, description, stock, price: from the JLC parts library (without internet the xlsx leaves them empty)
            int notFetched;
            var info = JlcParts.Fetch(env, bom.Where(x => IsValidCode(x.Lcsc)).Select(x => x.Lcsc), out notFetched);
            if (notFetched > 0) notes.Add(Tx.PartsNotFetched(notFetched));

            // Cost as JLC charges it: quantities with attrition and minimum quantity, assembly fees of the assembly type
            // (parts on the bottom side: both sides, Standard PCBA). Joints are counted on the placed footprints.
            bool bothSides = cpl.Any(x => x.Side == "Bottom");
            int smtJoints = 0, thtJoints = 0, leadless = 0;
            foreach (var x in cpl)
            {
                BoardFootprint f;
                if (!board.TryGetValue(x.Ref, out f)) continue;
                smtJoints += f.SmdPads; thtJoints += f.ThtPads;
                if (JlcCost.IsLeadless(f.Id)) leadless++;
            }
            var estimate = JlcCost.Estimate(placed, info, boards, bothSides, smtJoints, thtJoints, leadless);
            foreach (var l in estimate.Lines.Where(l => l.LowStock))
                notes.Add(Tx.LowStock(l.Row.Lcsc, l.Row.Value, l.Info.Stock, boards, l.Charged));
            if (estimate.UnknownRule > 0) notes.Add(Tx.AttritionUnknown(estimate.UnknownRule));

            string xlsx = Path.Combine(outDir, XlsxFile(p));
            Xlsx.Write(xlsx, p, m, s, r, bom, cpl, info, boards, estimate);
            files.Add(xlsx);
            return files;
        }
    }
}
