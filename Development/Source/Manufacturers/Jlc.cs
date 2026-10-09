using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;

namespace KiCadDrc
{
    // ------------------------------------------------------------------ JLCPCB
    // Source: https://jlcpcb.com/capabilities/pcb-capabilities
    // Stackups: JLC impedance calculator API (refreshed with update-stackups.js)

    class Jlc : IManufacturer
    {
        public string Code { get { return "JLC"; } }
        public string Name { get { return "JLCPCB"; } }
        public string Source { get { return "jlcpcb.com/capabilities/pcb-capabilities"; } }

        readonly List<Stackup> db;

        public Jlc() { db = LoadStackups(); }

        static List<Stackup> LoadStackups()
        {
            var result = new List<Stackup>();
            using (var gz = new GZipStream(Assembly.GetExecutingAssembly().GetManifestResourceStream("stackups.json.gz"), CompressionMode.Decompress))
            using (var r = new StreamReader(gz, Encoding.UTF8))
            {
                var items = (object[])S.Json().DeserializeObject(r.ReadToEnd());
                string previous = null;
                foreach (IDictionary<string, object> d in items)
                {
                    var st = new Stackup
                    {
                        Name = (string)d["name"], L = Convert.ToInt32(d["L"]), Thickness = S.D(d["thick"]),
                        Outer = S.D(d["outer"]), Inner = S.D(d["inner"])
                    };
                    // The file is sorted by combination and JLC's order; the first of each combination is the default
                    string key = st.L + "|" + st.Thickness + "|" + st.Outer + "|" + st.Inner;
                    st.IsDefault = key != previous; previous = key;
                    foreach (object[] k in (object[])d["layers"])
                    {
                        if (k.Length == 1) st.Layers.Add(new StackLayer { IsCopper = true, Type = "copper", Thickness = S.D(k[0]) });
                        else st.Layers.Add(new StackLayer { Type = Convert.ToInt32(k[0]) == 1 ? "core" : "prepreg", Thickness = S.D(k[1]), Dk = S.D(k[2]), Material = (string)k[3] });
                    }
                    result.Add(st);
                }
            }
            return result;
        }

        public int[] LayerCounts()
        {
            return new[] { 2 }.Concat(db.Select(s => s.L).Distinct().OrderBy(x => x)).ToArray();
        }

        public double[] Thicknesses(int L)
        {
            if (L == 2) return new[] { 0.4, 0.6, 0.8, 1.0, 1.2, 1.6, 2.0 };
            return db.Where(s => s.L == L).Select(s => s.Thickness).Distinct().OrderBy(x => x).ToArray();
        }

        public double[] OuterCoppers(int L, double thickness)
        {
            if (L == 2) return new[] { 1, 2, 2.5, 3.5, 4.5 };
            return db.Where(s => s.L == L && s.Thickness == thickness).Select(s => s.Outer).Distinct().OrderBy(x => x).ToArray();
        }

        public double[] InnerCoppers(int L, double thickness, double outer)
        {
            if (L == 2) return new double[0];
            return db.Where(s => s.L == L && s.Thickness == thickness && s.Outer == outer).Select(s => s.Inner).Distinct().OrderBy(x => x).ToArray();
        }

        public List<Stackup> Stackups(int L, double thickness, double outer, double inner)
        {
            if (L == 2)
            {
                // JLC 2 layer: FR4 Dk 4.5 ("FR-4 Dielectric Constants" on the page)
                double cu = S.R(outer * 0.035);
                var st = new Stackup { Name = "FR4 " + S.F(thickness) + " mm", L = 2, Thickness = thickness, Outer = outer, IsDefault = true };
                st.Layers.Add(new StackLayer { IsCopper = true, Type = "copper", Thickness = cu });
                st.Layers.Add(new StackLayer { Type = "core", Thickness = S.R(thickness - 2 * cu), Dk = 4.5, Material = "FR4" });
                st.Layers.Add(new StackLayer { IsCopper = true, Type = "copper", Thickness = cu });
                return new List<Stackup> { st };
            }
            return db.Where(s => s.L == L && s.Thickness == thickness && s.Outer == outer && s.Inner == inner).ToList();
        }

        public Option[] Masks()
        {
            // Soldermask bridge (1 oz): 0.10 for colours, 0.13 for black/white
            return new[]
            {
                new Option("green", Tx.MaskName("green"), "Green", 0.10), new Option("red", Tx.MaskName("red"), "Red", 0.10),
                new Option("yellow", Tx.MaskName("yellow"), "Yellow", 0.10), new Option("blue", Tx.MaskName("blue"), "Blue", 0.10),
                new Option("purple", Tx.MaskName("purple"), "Purple", 0.10), new Option("black", Tx.MaskName("black"), "Black", 0.13),
                new Option("white", Tx.MaskName("white"), "White", 0.13),
            };
        }

        public Option[] Finishes(int L, double thickness)
        {
            var l = new List<Option>();
            // No HASL for 6+ layers and for boards 0.4 mm and thinner
            if (L < 6 && thickness > 0.4)
            {
                l.Add(new Option("hasl", Tx.FinishHasl, "HAL SnPb", 0));
                l.Add(new Option("hasl-lf", Tx.FinishHaslLeadFree, "HAL lead-free", 0));
            }
            l.Add(new Option("enig", "ENIG", "ENIG", 0));
            l.Add(new Option("osp", "OSP", "OSP", 0));
            return l.ToArray();
        }

        public Option[] Edges()
        {
            return new[]
            {
                new Option("vcut", Tx.EdgeVcut, "", 0.4),
                new Option("routed", Tx.EdgeRouted, "", 0.2),
            };
        }

        // Recommendations: JLC order defaults (1.6 mm, outer 1 oz, inner 0.5 oz, green mask, "No requirement" stackup),
        // JLC default finish (HASL up to 4 layers, ENIG from 6 layers since there is no HASL), safe edge (V-cut)
        public Selection Recommended(int L, double thickness, double outer, double inner)
        {
            if (!LayerCounts().Contains(L))
                throw new InvalidOperationException(Tx.LayerCountNotMade(Name, L, string.Join(", ", LayerCounts())));
            var s = new Selection { Maker = Code, Layers = L, Margin = 0.05, Mask = "green", Edge = "vcut" };
            var thicknesses = Thicknesses(L);
            s.Thickness = !double.IsNaN(thickness) ? thickness : thicknesses.Contains(1.6) ? 1.6 : thicknesses.Where(x => x >= 1.6).DefaultIfEmpty(thicknesses.Last()).First();
            var outers = OuterCoppers(L, s.Thickness);
            s.Outer = !double.IsNaN(outer) ? outer : outers.Contains(1) ? 1 : outers.FirstOrDefault();
            var inners = InnerCoppers(L, s.Thickness, s.Outer);
            s.Inner = !double.IsNaN(inner) ? inner : inners.Length == 0 ? 0 : inners.Contains(0.5) ? 0.5 : inners[0];
            var st = Stackups(L, s.Thickness, s.Outer, s.Inner).FirstOrDefault();
            s.Stackup = st == null ? null : st.Name;
            s.Finish = L < 6 && s.Thickness > 0.4 ? "hasl" : "enig";
            return s;
        }

        public string Hint(string field) { return Tx.JlcHint(field); }

        public RuleSet Calculate(Selection s)
        {
            var k = new RuleSet();
            // Above a 0.15 margin the rules contradict each other (e.g. via-in-pad max drill < min drill)
            double p = Math.Min(0.15, Math.Max(0, s.Margin));
            k.Margin = p;
            bool ml = s.Layers > 2;
            string groupName = ml ? Tx.Multilayer : Tx.TwoLayer;
            double oz = s.Outer, innerOz = ml ? s.Inner : 0;

            // --- track / clearance (page: Traces). If the inner copper is heavier the general minimum follows it
            Func<double, bool, double> trackLimit = (copper, multi) => multi
                ? (copper >= 2 ? 0.15 : 0.09)
                : (copper >= 4.5 ? 0.30 : copper >= 3.5 ? 0.25 : copper >= 2.5 ? 0.20 : copper >= 2 ? 0.16 : 0.10);
            double trackRaw = Math.Max(trackLimit(oz, ml), ml ? trackLimit(innerOz, true) : 0);
            string trackSource = "JLC " + groupName + " " + S.Oz(oz) + (ml && trackLimit(innerOz, true) > trackLimit(oz, true) ? " (" + Tx.InnerShort + " " + S.Oz(innerOz) + ")" : "") + ": " + S.F(trackRaw);
            double track = S.R(trackRaw + p);

            double edgeRaw = 0.4; string edgeName = "V-cut";
            foreach (var e in Edges()) if (e.Code == s.Edge) { edgeRaw = e.Value; edgeName = e.Code == "vcut" ? "V-cut" : Tx.RoutedShort; }

            double pthClearance = Math.Max(S.R(0.28 + p), 0.35);
            double minDrill = S.R(0.20 + p), minViaDiameter = S.R(0.45 + p), minViaRing = S.R(0.075 + p);
            // The default via always satisfies its own limits (grows with the margin)
            k.ViaDrill = Math.Max(0.3, S.CeilTo(minDrill, 0.05));
            k.ViaDiameter = Math.Max(0.6, S.CeilTo(Math.Max(minViaDiameter, k.ViaDrill + 2 * minViaRing), 0.05));

            Action<string, double, string> board = (key, value, source) =>
            {
                k.Board[key] = value;
                k.Rows.Add(new RuleRow("Board Setup", Tx.BoardRuleName(key), S.Mm(value), source));
            };
            k.Board.Put("max_error", 0.005).Put("min_groove_width", 0.0).Put("min_microvia_diameter", 0.2)
                .Put("min_microvia_drill", 0.1).Put("min_resolved_spokes", 2).Put("use_height_for_length_calcs", true);
            board("min_track_width", track, trackSource);
            board("min_clearance", track, trackSource);
            board("min_connection", track, trackSource);
            board("min_through_hole_diameter", minDrill, Tx.SrcMinDrill);
            board("min_via_diameter", minViaDiameter, Tx.SrcMinVia);
            board("min_via_annular_width", minViaRing, Tx.SrcViaRing);
            board("min_hole_clearance", pthClearance, Tx.SrcHoleClearance(ml));
            board("min_hole_to_hole", S.R(0.45 + p), Tx.SrcPadHoleToHole);
            board("min_copper_edge_clearance", S.R(edgeRaw + p), "JLC " + edgeName + ": " + S.F(edgeRaw));
            board("solder_mask_to_copper_clearance", S.R(0.09 + p), Tx.SrcMaskToTrack);
            board("min_silk_clearance", 0.15, Tx.SrcSilkClearance);
            board("min_text_height", 1.0, Tx.SrcTextHeight);
            board("min_text_thickness", 0.15, Tx.SrcTextThickness);

            // --- custom rules (.kicad_dru). In KiCad a later rule of the same constraint type overrides an earlier one,
            // and custom clearance rules override net classes, so no clearance rule is written.
            Action<string, string, string, string, string> custom = (id, condition, constraints, value, source) =>
            {
                var sb = new StringBuilder();
                sb.AppendFormat("(rule \"{0}: {1}\"\n", Name, Tx.Rule(id));
                if (condition != null) sb.AppendFormat("\t(condition \"{0}\")\n", condition);
                sb.Append('\t').Append(constraints.Replace("\n", "\n\t")).Append(')');
                k.Custom.Add(sb.ToString());
                k.Rows.Add(new RuleRow(Tx.GroupCustomRule, Tx.Rule(id), value, source));
            };
            double maxDrill = S.R(6.3 - p);
            custom("pth_hole", "A.Type == 'Pad' && A.isPlated()",
                "(constraint hole_size (min " + S.F(minDrill) + "mm) (max " + S.F(maxDrill) + "mm))",
                S.F(minDrill) + " - " + S.Mm(maxDrill), Tx.SrcDrillRange);
            custom("pth_min_warning", "A.Type == 'Pad' && A.isPlated()",
                "(constraint assertion \"A.Hole_Size_X >= 0.5mm && A.Hole_Size_Y >= 0.5mm\")\n(severity warning)",
                "0.5 mm", Tx.SrcPthWarning);
            double viaMax = Math.Max(S.R(0.55 - p), k.ViaDrill);
            if (s.Layers >= 6)
            {
                k.Limits["via_max"] = viaMax;
                custom("via_in_pad", "A.Type == 'Via'",
                    "(constraint hole_size (min " + S.F(minDrill) + "mm) (max " + S.F(viaMax) + "mm))",
                    "max " + S.Mm(viaMax), Tx.SrcViaInPad);
            }
            custom("npth_hole", "A.Pad_Type == 'NPTH, mechanical'",
                "(constraint hole_size (min " + S.F(S.R(0.5 + p)) + "mm))\n(constraint hole_clearance (min " + S.F(S.R(0.2 + p)) + "mm))",
                "min " + S.Mm(S.R(0.5 + p)) + ", " + Tx.TrackShort + " " + S.Mm(S.R(0.2 + p)), Tx.SrcNpth);
            double slot = ml ? 0.35 : 0.5;
            k.Limits["track"] = track; k.Limits["npth_min"] = S.R(0.5 + p); k.Limits["slot_min"] = S.R(slot + p);
            k.Limits["npth_slot_min"] = S.R(1.0 + p); k.Limits["max_drill"] = maxDrill;
            k.Limits["cast_min"] = S.R(0.5 + p); k.Limits["via_h2h"] = S.R(0.2 + p); k.Limits["via_hole_track"] = S.R(0.2 + p);
            k.Limits["pad_h2h"] = S.R(0.45 + p);
            custom("plated_slot", "A.Type == 'Pad' && A.isPlated() && A.Hole_Size_X != A.Hole_Size_Y",
                "(constraint hole_size (min " + S.F(S.R(slot + p)) + "mm) (max " + S.F(maxDrill) + "mm))\n(constraint assertion \"A.Hole_Size_X >= 2 * A.Hole_Size_Y || A.Hole_Size_Y >= 2 * A.Hole_Size_X\")",
                "min " + S.Mm(S.R(slot + p)) + ", " + Tx.SlotLengthRule, Tx.SrcPlatedSlot(groupName, slot));
            custom("npth_slot", "A.Pad_Type == 'NPTH, mechanical' && A.Hole_Size_X != A.Hole_Size_Y",
                "(constraint hole_size (min " + S.F(S.R(1.0 + p)) + "mm))\n(constraint hole_clearance (min " + S.F(S.R(0.2 + p)) + "mm))",
                "min " + S.Mm(S.R(1.0 + p)), Tx.SrcNpthSlot);
            custom("castellated_hole", "A.Fabrication_Property == 'Castellated pad'",
                "(constraint hole_size (min " + S.F(S.R(0.5 + p)) + "mm))",
                "min " + S.Mm(S.R(0.5 + p)), Tx.SrcCastellated);
            custom("castellated_spacing", "A.Fabrication_Property == 'Castellated pad' && B.Fabrication_Property == 'Castellated pad'",
                "(constraint hole_to_hole (min " + S.F(S.R(0.5 + p)) + "mm))",
                S.Mm(S.R(0.5 + p)), Tx.SrcCastellatedSpacing);
            double ringMin, ringRecommended;
            if (Math.Max(oz, innerOz) >= 2) { ringMin = 0.254; ringRecommended = 0; }
            else if (ml) { ringMin = 0.15; ringRecommended = 0.20; }
            else { ringMin = 0.18; ringRecommended = 0.25; }
            double ring = Math.Max(S.R(ringMin + p), ringRecommended);
            k.Limits["ring"] = ring;
            // Plated pads with holes below 0.5 mm (thermal vias etc.) behave like vias: the Board Setup via ring rule applies
            custom("pth_ring", "A.Type == 'Pad' && A.isPlated() && (A.Hole_Size_X >= 0.5mm || A.Hole_Size_Y >= 0.5mm)",
                "(constraint annular_width (min " + S.F(ring) + "mm))",
                S.Mm(ring), Tx.SrcPthRing(groupName, Math.Max(oz, innerOz) >= 2, ringMin, ringRecommended));
            custom("npth_ring", "A.Pad_Type == 'NPTH, mechanical' && A.Size_X > A.Hole_Size_X",
                "(constraint assertion \"A.Size_X - A.Hole_Size_X >= 0.9mm && A.Size_Y - A.Hole_Size_Y >= 0.9mm\")",
                "0.45 mm", Tx.SrcNpthRing);
            custom("via_hole_to_hole", "A.Type == 'Via' && B.Type == 'Via'",
                "(constraint hole_to_hole (min " + S.F(S.R(0.2 + p)) + "mm))",
                S.Mm(S.R(0.2 + p)), "JLC via hole-to-hole 0.2");
            custom("via_hole_track", "A.Type == 'Via'",
                "(constraint hole_clearance (min " + S.F(S.R(0.2 + p)) + "mm))",
                S.Mm(S.R(0.2 + p)), "JLC via hole to track 0.2" + (ml ? " (" + Tx.InnerLayerShort + " 0.2)" : ""));
            custom("min_smd_pad", "A.Type == 'Pad' && A.Pad_Type == 'SMD'",
                "(constraint assertion \"A.Size_X >= 0.25mm && A.Size_Y >= 0.25mm\")\n(severity warning)",
                "0.25 × 0.25 mm", Tx.SrcMinSmdPad);
            custom("no_blind_vias", null,
                "(constraint disallow buried_via blind_via micro_via)",
                Tx.Forbidden, Tx.SrcNoBlind);

            // --- net class and preset sizes
            k.Track = Math.Max(0.25, S.CeilTo(track + 0.1, 0.05));
            k.Clearance = Math.Max(0.2, Math.Max(S.CeilTo(track + 0.05, 0.05), S.R(0.15 + p)));
            k.DpWidth = Math.Max(0.2, track); k.DpGap = k.Clearance;
            // Preset track widths: start at the manufacturer limit (min track rounded up to 0.01), then standard widths
            double firstTrack = S.CeilTo(track, 0.01);
            k.TrackPresets = new[] { firstTrack }.Concat(new[] { 0.2, 0.25, 0.3, 0.4, 0.5, 0.8, 1.0, 1.5, 2.0 }.Where(x => x > firstTrack)).ToArray();
            // Preset vias: 3 sizes from the manufacturer limit (0.05 steps) + the net class default; all satisfy the rules
            double d0 = S.CeilTo(minDrill, 0.05), c0 = S.CeilTo(Math.Max(minViaDiameter, d0 + 2 * minViaRing), 0.05);
            k.ViaPresets = new[] { new[] { c0, d0 }, new[] { S.R(c0 + 0.05), S.R(d0 + 0.05) }, new[] { S.R(c0 + 0.1), S.R(d0 + 0.1) }, new[] { k.ViaDiameter, k.ViaDrill } }
                .Where(v => v[1] >= minDrill && v[0] >= minViaDiameter && S.R((v[0] - v[1]) / 2) >= minViaRing && (s.Layers < 6 || v[1] <= viaMax))
                .GroupBy(v => v[0] + "/" + v[1]).Select(g => g.First()).OrderBy(v => v[0]).ThenBy(v => v[1]).ToArray();
            k.Rows.Add(new RuleRow(Tx.GroupPresets, Tx.TrackWidths, string.Join(", ", k.TrackPresets.Select(S.F)), Tx.StartsAtLimit));
            k.Rows.Add(new RuleRow(Tx.GroupPresets, Tx.ViaDiameterDrill, string.Join(", ", k.ViaPresets.Select(v => S.F(v[0]) + "/" + S.F(v[1]))), Tx.StartsAtLimitAllValid));
            k.Rows.Add(new RuleRow("Net class (Default)", Tx.TrackClearance, S.F(k.Track) + " / " + S.Mm(k.Clearance), Tx.SrcNetClassClearance));
            k.Rows.Add(new RuleRow("Net class (Default)", Tx.ViaDiameterDrillShort, S.F(k.ViaDiameter) + " / " + S.Mm(k.ViaDrill), Tx.SrcNetClassVia));

            // --- template board settings
            Option mask = Masks().FirstOrDefault(m => m.Code == s.Mask) ?? Masks()[0];
            k.MaskBridge = oz >= 2 ? 0.20 : mask.Value;
            k.MaskColor = mask.KiCad; k.SilkColor = mask.Code == "white" ? "Black" : "White";
            Option finish = Finishes(s.Layers, s.Thickness).FirstOrDefault(y => y.Code == s.Finish) ?? Finishes(s.Layers, s.Thickness)[0];
            k.FinishKiCad = finish.KiCad;
            k.ViaFill = s.Layers >= 6;
            k.HatchThickness = k.HatchGap = S.R(0.25 + p);
            k.ZoneMinThickness = Math.Max(0.25, track);
            k.Stackup = Stackups(s.Layers, s.Thickness, s.Outer, s.Inner).FirstOrDefault(x => x.Name == s.Stackup)
                        ?? Stackups(s.Layers, s.Thickness, s.Outer, s.Inner).FirstOrDefault();
            if (k.Stackup == null) throw new InvalidOperationException(Tx.NoStackupForCombination);

            string g2 = Tx.GroupTemplate;
            k.Rows.Add(new RuleRow(g2, "Stackup", k.Stackup.Name, Tx.StackupSource(s.Layers, S.Mm(k.Stackup.Total), S.Mm(s.Thickness))));
            k.Rows.Add(new RuleRow(g2, Tx.MaskBridge, S.Mm(k.MaskBridge), oz >= 2 ? "JLC 2 oz: 0.20" : "JLC 1 oz " + mask.Name.ToLower() + ": " + S.F(mask.Value)));
            k.Rows.Add(new RuleRow(g2, Tx.MaskExpansion, "1:1 (0 mm)", "JLC soldermask expansion 1:1"));
            k.Rows.Add(new RuleRow(g2, Tx.MaskSilkColor, mask.Name + " / " + (k.SilkColor == "White" ? Tx.White : Tx.Black), Tx.View3D));
            k.Rows.Add(new RuleRow(g2, Tx.SurfaceFinish, finish.Name, s.Layers >= 6 ? Tx.SrcNoHasl6 : ""));
            k.Rows.Add(new RuleRow(g2, "Via", k.ViaFill ? Tx.ViaFilledTented : "tented", k.ViaFill ? Tx.SrcViaInPadDefault : Tx.JlcDefault));
            k.Rows.Add(new RuleRow(g2, Tx.ZoneHatch, S.Mm(k.HatchThickness), "JLC hatched grid 0.25"));
            k.Rows.Add(new RuleRow(g2, Tx.SilkText, "1.0 / 0.17 mm", Tx.SrcSilkRatio));

            if (s.Layers > 10) k.Notes.Add(Tx.NoteEngineering);
            if (!ml && oz > 2) k.Notes.Add(Tx.NoteHeavyCopper);
            if (s.Thickness >= 2.5 && s.Layers < 12) k.Notes.Add(Tx.NoteThick);
            if (finish.Code != "enig") k.Notes.Add(Tx.NoteBga);
            return k;
        }
    }
}
