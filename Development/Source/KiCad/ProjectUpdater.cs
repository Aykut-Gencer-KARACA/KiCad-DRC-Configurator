using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace KiCadDrc
{
    // ------------------------------------------------------------------ updating an existing project
    // Applies the selected manufacturer settings (layer count, stackup, rules) to an existing project. Drawings, layer
    // names, net classes and the user's own custom rules are not touched. When reducing layers nothing is done if a
    // layer to be removed has even one item. A backup is taken first; if item counts differ afterwards it is restored.

    class ProjectInfo
    {
        public string ProPath, PcbPath, DruPath, Name, Dir;
        public int Layers;                                                           // current copper layer count
        public Dictionary<int, int> InnerUsage = new Dictionary<int, int>();         // inner layer no -> items on it
        public Dictionary<string, int> Items = new Dictionary<string, int>();        // root level item type -> count
        public List<string> Locks = new List<string>();
        public List<string> StaleLocks = new List<string>();   // lock files left behind while KiCad is closed (full paths)
        // Read from the PCB stackup (to sync the left panel with the project); NaN / null when not found
        public double Thickness = double.NaN, OuterCopper = double.NaN, InnerCopper = double.NaN;
        public string Revision = "—";   // rev from the PCB title block, otherwise a suffix like "RevC" in the folder name
        public List<double> Dielectrics = new List<double>();
        public string MaskColor, Finish;
    }

    class UpdatePlan
    {
        public int From, To;
        public List<string> Added = new List<string>(), Removed = new List<string>();
        public string Blocker;   // when set the update cannot be done
    }

    class UpdateResult
    {
        public string Backup;
        public List<string> Changes = new List<string>();
    }

    static class ProjectUpdater
    {
        const string BlockStart = "# --- KiCad DRC Configurator: begin", BlockEnd = "# --- KiCad DRC Configurator: end ---";
        static readonly Regex CopperLine = new Regex(@"^\(\s*\d+\s+""((?:F|B|In\d+)\.Cu)""");
        static readonly Regex InnerLayerName = new Regex(@"""In(\d+)\.Cu""");
        // Root level sections that are not drawings
        static readonly string[] HeaderSections = { "version", "generator", "generator_version", "general", "paper", "title_block", "layers", "setup", "property", "net", "embedded_fonts", "embedded_files" };

        public static ProjectInfo Analyze(string proPath)
        {
            var p = new ProjectInfo { ProPath = Path.GetFullPath(proPath) };
            p.Dir = Path.GetDirectoryName(p.ProPath);
            p.Name = Path.GetFileNameWithoutExtension(p.ProPath);
            p.PcbPath = Path.Combine(p.Dir, p.Name + ".kicad_pcb");
            p.DruPath = Path.Combine(p.Dir, p.Name + ".kicad_dru");
            if (!File.Exists(p.PcbPath)) throw new InvalidOperationException(Tx.PcbNotFound(p.PcbPath));
            // KiCad creates ~name.ext.lck for open files. If KiCad crashes they stay behind:
            // when no KiCad process runs the locks count as stale (they are deleted during the update)
            var locks = Directory.GetFiles(p.Dir, "~" + p.Name + ".*.lck");
            bool kicadRunning = new[] { "kicad", "pcbnew", "eeschema" }.Any(n => System.Diagnostics.Process.GetProcessesByName(n).Length > 0);
            if (kicadRunning) p.Locks.AddRange(locks.Select(Path.GetFileName));
            else p.StaleLocks.AddRange(locks);

            string t = File.ReadAllText(p.PcbPath, Encoding.UTF8);
            var root = Sexp.Root(t, "kicad_pcb");
            foreach (var c in Sexp.Children(t, root))
            {
                if (c.Name == "layers")
                    p.Layers = Sexp.Children(t, c).Count(k => CopperLine.IsMatch(k.Text(t)));
                if (c.Name == "general")
                {
                    var th = Sexp.Children(t, c).FirstOrDefault(x => x.Name == "thickness");
                    if (th != null) p.Thickness = ReadNumber(th.Text(t));
                }
                if (c.Name == "setup") ReadStackup(t, c, p);
                if (c.Name == "title_block")
                {
                    var rev = Regex.Match(c.Text(t), @"\(rev\s+""([^""]+)""");
                    if (rev.Success && rev.Groups[1].Value.Trim().Length > 0) p.Revision = rev.Groups[1].Value.Trim();
                }
                if (HeaderSections.Contains(c.Name)) continue;
                int count; p.Items.TryGetValue(c.Name, out count); p.Items[c.Name] = count + 1;
                // Which inner layers this item uses (an item counts a layer once)
                foreach (int n in InnerLayerName.Matches(c.Text(t)).Cast<Match>().Select(m => int.Parse(m.Groups[1].Value, S.Inv)).Distinct())
                {
                    int u; p.InnerUsage.TryGetValue(n, out u); p.InnerUsage[n] = u + 1;
                }
            }
            if (p.Layers < 2) throw new InvalidOperationException(Tx.NoCopperLayers(p.PcbPath));
            if (p.Revision == "—")
            {
                var rev = Regex.Match(Path.GetFileName(p.Dir), @"rev[\s_-]*([A-Za-z0-9]+)", RegexOptions.IgnoreCase);
                if (rev.Success) p.Revision = rev.Groups[1].Value.ToUpperInvariant();
            }
            return p;
        }

        static double ReadNumber(string block)
        {
            var m = Regex.Match(block, @"-?\d+(?:\.\d+)?");
            return m.Success ? double.Parse(m.Value, S.Inv) : double.NaN;
        }

        // (setup (stackup ...)): copper thicknesses, dielectric thicknesses (sub-layers summed), mask colour, finish
        static void ReadStackup(string t, Sexp.Node setup, ProjectInfo p)
        {
            var st = Sexp.Children(t, setup).FirstOrDefault(x => x.Name == "stackup");
            if (st == null) return;
            var nameRe = new Regex(@"^\(layer\s+""([^""]+)""");
            var typeRe = new Regex(@"\(type\s+""([^""]+)""\)");
            var thickRe = new Regex(@"\(thickness\s+([\d.]+)");
            var colorRe = new Regex(@"\(color\s+""([^""]+)""\)");
            foreach (var c in Sexp.Children(t, st))
            {
                string block = c.Text(t);
                if (c.Name == "copper_finish") { var m = Regex.Match(block, @"""([^""]+)"""); if (m.Success) p.Finish = m.Groups[1].Value; continue; }
                if (c.Name != "layer") continue;
                string layer = nameRe.Match(block).Groups[1].Value, type = typeRe.Match(block).Groups[1].Value;
                double total = thickRe.Matches(block).Cast<Match>().Sum(m => double.Parse(m.Groups[1].Value, S.Inv));
                if (layer == "F.Cu") p.OuterCopper = total;
                else if (layer == "In1.Cu") p.InnerCopper = total;
                else if (layer == "F.Mask") { var r = colorRe.Match(block); if (r.Success) p.MaskColor = r.Groups[1].Value; }
                else if (type == "prepreg" || type == "core") p.Dielectrics.Add(Math.Round(total, 4));
            }
        }

        // Manufacturer selection matching the project settings (the left panel is synced with the project). Edge and margin
        // are not stored in the project, so they are kept from the given selection. The stackup is the JLC stackup with
        // matching dielectric thicknesses.
        public static Selection SelectionFromProject(IManufacturer m, ProjectInfo p, Selection current, out string note)
        {
            note = null;
            if (!m.LayerCounts().Contains(p.Layers)) { note = Tx.LayerCountNotMadeKept(m.Name, p.Layers); return null; }
            var thicknesses = m.Thicknesses(p.Layers);
            double th = double.IsNaN(p.Thickness) ? double.NaN : thicknesses.OrderBy(x => Math.Abs(x - p.Thickness)).First();
            Func<double, double> oz = mm => double.IsNaN(mm) ? double.NaN : Math.Round(mm / 0.035 * 2) / 2;
            var s = m.Recommended(p.Layers, th, double.NaN, double.NaN);
            double outer = oz(p.OuterCopper);
            if (!double.IsNaN(outer) && m.OuterCoppers(p.Layers, s.Thickness).Contains(outer)) s = m.Recommended(p.Layers, s.Thickness, outer, double.NaN);
            double inner = oz(p.InnerCopper);
            if (p.Layers > 2 && !double.IsNaN(inner) && m.InnerCoppers(p.Layers, s.Thickness, s.Outer).Contains(inner)) s = m.Recommended(p.Layers, s.Thickness, s.Outer, inner);
            var match = m.Stackups(p.Layers, s.Thickness, s.Outer, s.Inner).FirstOrDefault(x =>
            {
                var d = x.Layers.Where(k => !k.IsCopper).Select(k => k.Thickness).ToList();
                return d.Count == p.Dielectrics.Count && d.Zip(p.Dielectrics, (a, b) => Math.Abs(a - b) < 0.002).All(y => y);
            });
            if (match != null) s.Stackup = match.Name;
            else if (p.Layers > 2) note = Tx.StackupNotMatched(m.Name);
            var mask = m.Masks().FirstOrDefault(x => string.Equals(x.KiCad, p.MaskColor, StringComparison.OrdinalIgnoreCase));
            if (mask != null) s.Mask = mask.Code;
            var finish = m.Finishes(p.Layers, s.Thickness).FirstOrDefault(y => string.Equals(y.KiCad, p.Finish, StringComparison.OrdinalIgnoreCase));
            if (finish != null) s.Finish = finish.Code;
            s.Edge = current.Edge; s.Margin = current.Margin;
            return s;
        }

        // Like KiCad: new inner layers are added at the end (above B.Cu), reducing removes the last inner layers
        public static UpdatePlan Plan(ProjectInfo p, int target)
        {
            var plan = new UpdatePlan { From = p.Layers, To = target };
            for (int i = p.Layers - 1; i <= target - 2; i++) plan.Added.Add("In" + i + ".Cu");
            for (int i = target - 1; i <= p.Layers - 2; i++) plan.Removed.Add("In" + i + ".Cu");
            var used = plan.Removed.Select(name =>
                {
                    int n; p.InnerUsage.TryGetValue(Files.InnerNumber(name), out n);
                    return new { name, n };
                }).Where(x => x.n > 0).ToList();
            if (used.Count > 0) plan.Blocker = Tx.LayersNotEmpty(used.Select(x => Tuple.Create(x.name, x.n)).ToList());
            return plan;
        }

        public static UpdateResult Update(KiCadEnvironment env, IManufacturer m, Selection s, RuleSet r, ProjectInfo p)
        {
            if (p.Locks.Count > 0) throw new InvalidOperationException(Tx.ProjectOpenInKiCad(string.Join(", ", p.Locks)));
            var plan = Plan(p, s.Layers);
            if (plan.Blocker != null) throw new InvalidOperationException(plan.Blocker);
            // Stale locks from a closed KiCad are removed (so KiCad does not report "file locked" next time)
            foreach (var l in p.StaleLocks) { try { File.Delete(l); } catch (IOException) { } catch (UnauthorizedAccessException) { } }

            var result = new UpdateResult();
            result.Backup = Path.Combine(env.DataDir, DiskNames.ProjectBackups, p.Name + "_" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss"));
            Directory.CreateDirectory(result.Backup);
            bool hadDru = File.Exists(p.DruPath);
            foreach (var path in new[] { p.ProPath, p.PcbPath, p.DruPath }.Where(File.Exists))
                File.Copy(path, Path.Combine(result.Backup, Path.GetFileName(path)));

            try
            {
                string pcb = UpdatePcb(File.ReadAllText(p.PcbPath, Encoding.UTF8), r, plan, result.Changes);
                string pro = UpdatePro(File.ReadAllText(p.ProPath, Encoding.UTF8), r, result.Changes);
                string dru = UpdateDru(hadDru ? File.ReadAllText(p.DruPath, Encoding.UTF8) : null, m, s, r);
                result.Changes.Add(hadDru ? Tx.ChangeDruRenewed(p.Name) : Tx.ChangeDruCreated(p.Name));
                var utf8 = new UTF8Encoding(false);
                File.WriteAllText(p.PcbPath, pcb, utf8);
                File.WriteAllText(p.ProPath, pro, utf8);
                File.WriteAllText(p.DruPath, dru, utf8);

                // Safety check: layer count must be the target and no item may be lost
                var after = Analyze(p.ProPath);
                if (after.Layers != s.Layers) throw new InvalidOperationException(Tx.LayerCountAfterUpdate(after.Layers, s.Layers));
                foreach (var type in p.Items.Keys.Union(after.Items.Keys))
                {
                    int before, now; p.Items.TryGetValue(type, out before); after.Items.TryGetValue(type, out now);
                    if (before != now) throw new InvalidOperationException(Tx.ItemCountChanged(type, before, now));
                }
            }
            catch (Exception)
            {
                // The project is restored from the backup
                foreach (var path in new[] { p.ProPath, p.PcbPath, p.DruPath })
                {
                    string b = Path.Combine(result.Backup, Path.GetFileName(path));
                    if (File.Exists(b)) File.Copy(b, path, true);
                }
                if (!hadDru && File.Exists(p.DruPath)) File.Delete(p.DruPath);
                throw;
            }
            return result;
        }

        static string UpdatePcb(string t, RuleSet r, UpdatePlan plan, List<string> changes)
        {
            string nl = t.Contains("\r\n") ? "\r\n" : "\n";   // the file's line ending style is kept
            Func<string, string> lines = x => x.Replace("\r\n", "\n").Replace("\n", nl);
            var root = Sexp.Root(t, "kicad_pcb");
            var children = Sexp.Children(t, root);
            var edits = new List<Tuple<int, int, string>>();   // start, end, new text

            // (layers): existing copper lines (including the user's layer names/types) are kept, new ones are added
            var layers = children.First(c => c.Name == "layers");
            var copper = new Dictionary<string, string>(); var other = new List<string>();
            foreach (var c in Sexp.Children(t, layers))
            {
                string text = c.Text(t);
                var m = CopperLine.Match(text);
                if (m.Success) copper[m.Groups[1].Value] = text; else other.Add(text);
            }
            var newLayers = new StringBuilder("(layers\n");
            foreach (var name in Files.CopperNames(plan.To))
                newLayers.Append("\t\t").Append(copper.ContainsKey(name) ? copper[name] : "(" + Files.CopperId(name) + " \"" + name + "\" signal)").Append('\n');
            foreach (var d in other) newLayers.Append("\t\t").Append(d).Append('\n');
            newLayers.Append("\t)");
            edits.Add(Tuple.Create(layers.Start, layers.End, lines(newLayers.ToString())));
            if (plan.From != plan.To) changes.Add(Tx.ChangeLayers(plan.From, plan.To, plan.Added, plan.Removed));

            // (setup): stackup and mask/via settings; other setup settings are not touched
            var setup = children.FirstOrDefault(c => c.Name == "setup");
            if (setup == null) throw new InvalidOperationException(Tx.NoSetupSection);
            var setupChildren = Sexp.Children(t, setup);
            var stackup = setupChildren.FirstOrDefault(c => c.Name == "stackup");
            string newStackup = lines(Files.StackupBlock(r).TrimStart('\t'));
            var append = new StringBuilder();
            if (stackup != null) edits.Add(Tuple.Create(stackup.Start, stackup.End, newStackup));
            else append.Append("\t\t").Append(newStackup).Append(nl);
            changes.Add(Tx.ChangeStackup(r.Stackup.Name, S.Mm(Files.BoardThickness(r)), r.FinishKiCad));
            string yes = r.ViaFill ? "yes" : "no";
            foreach (var setting in new[] { Tuple.Create("pad_to_mask_clearance", "0"), Tuple.Create("solder_mask_min_width", S.F(r.MaskBridge)),
                                            Tuple.Create("capping", yes), Tuple.Create("filling", yes) })
            {
                var c = setupChildren.FirstOrDefault(x => x.Name == setting.Item1);
                string value = "(" + setting.Item1 + " " + setting.Item2 + ")";
                if (c != null) edits.Add(Tuple.Create(c.Start, c.End, value));
                else append.Append("\t\t").Append(value).Append(nl);
            }
            if (append.Length > 0)
            {
                // Inserted at the start of the line holding setup's closing parenthesis (empty range = pure insert)
                int lineStart = t.LastIndexOf('\n', setup.End) + 1;
                edits.Add(Tuple.Create(lineStart, lineStart - 1, append.ToString()));
            }
            changes.Add(Tx.ChangeMask(S.Mm(r.MaskBridge), r.ViaFill));

            // (general (thickness ...))
            var general = children.FirstOrDefault(c => c.Name == "general");
            if (general != null)
            {
                var th = Sexp.Children(t, general).FirstOrDefault(c => c.Name == "thickness");
                if (th != null) edits.Add(Tuple.Create(th.Start, th.End, "(thickness " + S.F(Files.BoardThickness(r)) + ")"));
            }

            // Edits are applied from the end so earlier positions do not shift
            var sb = new StringBuilder(t);
            foreach (var d in edits.OrderByDescending(x => x.Item1))
            {
                sb.Remove(d.Item1, d.Item2 - d.Item1 + 1);
                sb.Insert(d.Item1, d.Item3);
            }
            return sb.ToString();
        }

        // .kicad_pro: only rules, preset sizes and (if needed) the lower limits of the Default net class change
        static string UpdatePro(string text, RuleSet r, List<string> changes)
        {
            var root = S.Json().DeserializeObject(text) as Dictionary<string, object>;
            if (root == null) throw new InvalidOperationException(Tx.ProUnreadable);
            Func<Dictionary<string, object>, string, Dictionary<string, object>> sub = (d, name) =>
            {
                if (!d.ContainsKey(name) || !(d[name] is Dictionary<string, object>)) d[name] = new Dictionary<string, object>();
                return (Dictionary<string, object>)d[name];
            };
            var ds = sub(sub(root, "board"), "design_settings");
            var rules = sub(ds, "rules");
            int changed = 0;
            foreach (DictionaryEntry e in r.Board)
            {
                string name = (string)e.Key;
                if (!rules.ContainsKey(name) || !SameValue(rules[name], e.Value)) changed++;
                rules[name] = e.Value;
            }
            changes.Add(Tx.ChangeBoardRules(changed, S.Mm(Convert.ToDouble(r.Board["min_track_width"]))));

            // Preset track and via sizes are replaced by the manufacturer list (the first 0 entry means "use net class" in KiCad)
            ds["track_widths"] = new object[] { 0.0 }.Concat(r.TrackPresets.Cast<object>()).ToArray();
            ds["via_dimensions"] = new object[] { new Dictionary<string, object> { { "diameter", 0.0 }, { "drill", 0.0 } } }
                .Concat(r.ViaPresets.Select(v => (object)new Dictionary<string, object> { { "diameter", v[0] }, { "drill", v[1] } })).ToArray();
            changes.Add(Tx.ChangePresets(string.Join(", ", r.TrackPresets.Select(S.F)), string.Join(", ", r.ViaPresets.Select(v => S.F(v[0]) + "/" + S.F(v[1])))));

            // Default net class: only values below the REAL limits are raised (valid values are not touched)
            var ns = root.ContainsKey("net_settings") ? root["net_settings"] as Dictionary<string, object> : null;
            var classes = ns != null && ns.ContainsKey("classes") ? ns["classes"] as object[] : null;
            var def = classes == null ? null : classes.OfType<Dictionary<string, object>>().FirstOrDefault(c => c.ContainsKey("name") && (string)c["name"] == "Default");
            if (def != null)
            {
                Func<string, double> rule = name => Convert.ToDouble(r.Board[name], S.Inv);
                var raised = new List<string>();
                Action<string, double, string> raise = (name, limit, label) =>
                {
                    if (def.ContainsKey(name) && Number(def, name) + 1e-9 < limit) { raised.Add(label + " " + S.F(Number(def, name)) + " → " + S.F(limit)); def[name] = limit; }
                };
                raise("track_width", rule("min_track_width"), Tx.TrackShort);
                // clearance: the larger of copper clearance and JLC SMD pad-pad (0.15 + margin)
                raise("clearance", Math.Max(rule("min_clearance"), S.R(0.15 + r.Margin)), Tx.ClearanceShort);
                if (def.ContainsKey("via_diameter") && def.ContainsKey("via_drill"))
                {
                    double vd = Number(def, "via_diameter"), vh = Number(def, "via_drill");
                    bool viaOk = vh + 1e-9 >= rule("min_through_hole_diameter") && vd + 1e-9 >= rule("min_via_diameter")
                        && (vd - vh) / 2 + 1e-9 >= rule("min_via_annular_width")
                        && (!r.Limits.ContainsKey("via_max") || vh <= r.Limits["via_max"] + 1e-9);
                    if (!viaOk)
                    {
                        raised.Add("via " + S.F(vd) + "/" + S.F(vh) + " → " + S.F(r.ViaDiameter) + "/" + S.F(r.ViaDrill));
                        def["via_diameter"] = r.ViaDiameter; def["via_drill"] = r.ViaDrill;
                    }
                }
                changes.Add(raised.Count == 0 ? Tx.ChangeNetClassOk : Tx.ChangeNetClassRaised(string.Join(", ", raised)));
            }
            return JsonWriter.Write(root);
        }

        static double Number(Dictionary<string, object> d, string name) { return d.ContainsKey(name) && d[name] != null ? Convert.ToDouble(d[name], S.Inv) : 0; }

        static bool SameValue(object a, object b)
        {
            if (a is bool || b is bool) return Equals(a, b);
            try { return Math.Abs(Convert.ToDouble(a, S.Inv) - Convert.ToDouble(b, S.Inv)) < 1e-9; } catch (Exception) { return false; }
        }

        // .kicad_dru: the app's block sits between markers, BEFORE the user's rules. In KiCad the later rule wins for the
        // same constraint, so the user's own rules take precedence over ours. Blocks written by older (Turkish) versions
        // are recognised by their old markers and replaced.
        public static string UpdateDru(string existing, IManufacturer m, Selection s, RuleSet r)
        {
            string block = BlockStart + " (" + m.Name + ", " + s.Layers + "L, " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ")\n" +
                "# " + Tx.DruBlockComment1 + "\n# " + Tx.DruBlockComment2 + "\n\n" +
                string.Join("\n\n", r.Custom) + "\n\n" + BlockEnd + "\n";
            if (existing == null) return "(version 1)\n\n" + block;

            string text = existing.Replace("\r\n", "\n");
            foreach (var markers in new[] { Tuple.Create(BlockStart, BlockEnd), Tuple.Create(Legacy.DruBlockStart, Legacy.DruBlockEnd) })
            {
                int start = text.IndexOf(markers.Item1, StringComparison.Ordinal);
                if (start < 0) continue;
                int end = text.IndexOf(markers.Item2, start, StringComparison.Ordinal);
                if (end < 0) throw new InvalidOperationException(Tx.DruBlockEndMissing);
                end += markers.Item2.Length;
                while (end < text.Length && text[end] == '\n') end++;
                text = text.Remove(start, end - start);
            }
            var version = Regex.Match(text, @"^\s*\(version\s+\d+\)[^\n]*\n*");
            if (!version.Success) return "(version 1)\n\n" + block + "\n" + text.TrimStart();
            return text.Substring(0, version.Length).TrimEnd('\n') + "\n\n" + block + (version.Length < text.Length ? "\n" + text.Substring(version.Length) : "");
        }
    }
}
