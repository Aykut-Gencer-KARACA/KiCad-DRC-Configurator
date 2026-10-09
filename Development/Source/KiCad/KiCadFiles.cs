using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace KiCadDrc
{
    // ------------------------------------------------------------------ KiCad file generation

    static class Files
    {
        public static string Pro(RuleSet r, string fileName)
        {
            var vias = new ArrayList { new OrderedMap().Put("diameter", 0.0).Put("drill", 0.0) };
            foreach (var v in r.ViaPresets) vias.Add(new OrderedMap().Put("diameter", v[0]).Put("drill", v[1]));
            var tracks = new ArrayList { 0.0 }; foreach (var i in r.TrackPresets) tracks.Add(i);

            var zone = new OrderedMap().Put("border_display_style", 2).Put("border_hatch_pitch", 0.5).Put("corner_radius", 0.0)
                .Put("corner_smoothing", 0).Put("fill_mode", 0).Put("hatch_gap", r.HatchGap).Put("hatch_orientation", 0.0)
                .Put("hatch_smoothing_level", 0).Put("hatch_smoothing_value", 0.1).Put("hatch_thickness", r.HatchThickness)
                .Put("min_clearance", 0.5).Put("min_island_area", 10.0).Put("min_thickness", r.ZoneMinThickness)
                .Put("pad_connection", 2).Put("remove_islands", 0).Put("thermal_relief_gap", 0.5).Put("thermal_relief_spoke_width", 0.5);

            var netClass = new OrderedMap().Put("bus_width", 12).Put("clearance", r.Clearance).Put("diff_pair_gap", r.DpGap)
                .Put("diff_pair_via_gap", r.DpViaGap).Put("diff_pair_width", r.DpWidth).Put("line_style", 0)
                .Put("microvia_diameter", 0.3).Put("microvia_drill", 0.1).Put("name", "Default")
                .Put("pcb_color", "rgba(0, 0, 0, 0.000)").Put("priority", 2147483647).Put("schematic_color", "rgba(0, 0, 0, 0.000)")
                .Put("track_width", r.Track).Put("tuning_profile", "").Put("via_diameter", r.ViaDiameter).Put("via_drill", r.ViaDrill).Put("wire_width", 6);

            var pro = new OrderedMap()
                .Put("board", new OrderedMap().Put("design_settings", new OrderedMap()
                    .Put("defaults", new OrderedMap().Put("silk_line_width", r.SilkLine).Put("silk_text_size_h", r.SilkText)
                        .Put("silk_text_size_v", r.SilkText).Put("silk_text_thickness", r.SilkTextThickness).Put("zones", zone))
                    .Put("diff_pair_dimensions", new ArrayList {
                        new OrderedMap().Put("gap", 0.0).Put("via_gap", 0.0).Put("width", 0.0),
                        new OrderedMap().Put("gap", r.DpGap).Put("via_gap", r.DpViaGap).Put("width", r.DpWidth) })
                    .Put("drc_exclusions", new ArrayList())
                    .Put("meta", new OrderedMap().Put("version", 2))
                    .Put("rules", r.Board)
                    .Put("track_widths", tracks)
                    .Put("via_dimensions", vias)))
                .Put("boards", new ArrayList())
                .Put("libraries", new OrderedMap().Put("pinned_footprint_libs", new ArrayList()).Put("pinned_symbol_libs", new ArrayList()))
                .Put("meta", new OrderedMap().Put("filename", fileName).Put("version", 3))
                .Put("net_settings", new OrderedMap().Put("classes", new ArrayList { netClass }).Put("meta", new OrderedMap().Put("version", 5))
                    .Put("net_colors", null).Put("netclass_assignments", null).Put("netclass_patterns", new ArrayList()))
                .Put("pcbnew", new OrderedMap().Put("page_layout_descr_file", ""))
                .Put("sheets", new ArrayList())
                .Put("text_variables", new OrderedMap());
            return JsonWriter.Write(pro);
        }

        // KiCad copper layer ids: F.Cu 0, B.Cu 2, InN.Cu 2+2N
        public static int CopperId(string name)
        {
            if (name == "F.Cu") return 0;
            if (name == "B.Cu") return 2;
            return 2 + 2 * InnerNumber(name);
        }

        // "In3.Cu" -> 3
        public static int InnerNumber(string name) { return int.Parse(name.Substring(2, name.Length - 5), S.Inv); }

        public static List<string> CopperNames(int n)
        {
            var l = new List<string> { "F.Cu" };
            for (int i = 1; i <= n - 2; i++) l.Add("In" + i + ".Cu");
            l.Add("B.Cu");
            return l;
        }

        // The (stackup ...) block inside (setup ...), indented with 2 tabs
        public static string StackupBlock(RuleSet r)
        {
            var st = r.Stackup;
            var copperNames = CopperNames(st.L);
            var sb = new StringBuilder("\t\t(stackup\n");
            Action<string, string, string> surface = (name, type, extra) =>
                sb.AppendFormat("\t\t\t(layer \"{0}\"\n\t\t\t\t(type \"{1}\")\n{2}\t\t\t)\n", name, type, extra);
            string silk = "\t\t\t\t(color \"" + r.SilkColor + "\")\n";
            string mask = "\t\t\t\t(color \"" + r.MaskColor + "\")\n\t\t\t\t(thickness 0.01)\n\t\t\t\t(epsilon_r 3.8)\n\t\t\t\t(loss_tangent 0)\n";
            surface("F.SilkS", "Top Silk Screen", silk);
            surface("F.Paste", "Top Solder Paste", "");
            surface("F.Mask", "Top Solder Mask", mask);
            int copper = 0, dielectric = 0;
            foreach (var layer in st.Layers)
            {
                if (layer.IsCopper)
                    sb.AppendFormat("\t\t\t(layer \"{0}\"\n\t\t\t\t(type \"copper\")\n\t\t\t\t(thickness {1})\n\t\t\t)\n", copperNames[copper++], S.F(layer.Thickness));
                else
                    sb.AppendFormat("\t\t\t(layer \"dielectric {0}\"\n\t\t\t\t(type \"{1}\")\n\t\t\t\t(thickness {2})\n\t\t\t\t(material \"{3}\")\n\t\t\t\t(epsilon_r {4})\n\t\t\t\t(loss_tangent 0.02)\n\t\t\t)\n",
                        ++dielectric, layer.Type, S.F(layer.Thickness), layer.Material, S.F(layer.Dk));
            }
            surface("B.Mask", "Bottom Solder Mask", mask);
            surface("B.Paste", "Bottom Solder Paste", "");
            surface("B.SilkS", "Bottom Silk Screen", silk);
            sb.AppendFormat("\t\t\t(copper_finish \"{0}\")\n\t\t\t(dielectric_constraints no)\n\t\t)", r.FinishKiCad);
            return sb.ToString();
        }

        public static double BoardThickness(RuleSet r) { return r.Stackup.Total + 0.02; }   // + two mask layers

        public static string Pcb(Selection s, RuleSet r)
        {
            int n = r.Stackup.L;
            var sb = new StringBuilder();
            sb.Append("(kicad_pcb\n\t(version 20260206)\n\t(generator \"pcbnew\")\n\t(generator_version \"10.0\")\n");
            sb.AppendFormat("\t(general\n\t\t(thickness {0})\n\t\t(legacy_teardrops no)\n\t)\n\t(paper \"A4\")\n\t(layers\n", S.F(BoardThickness(r)));
            foreach (var name in CopperNames(n)) sb.AppendFormat("\t\t({0} \"{1}\" signal)\n", CopperId(name), name);
            sb.Append(@"		(9 ""F.Adhes"" user ""F.Adhesive"")
		(11 ""B.Adhes"" user ""B.Adhesive"")
		(13 ""F.Paste"" user)
		(15 ""B.Paste"" user)
		(5 ""F.SilkS"" user ""F.Silkscreen"")
		(7 ""B.SilkS"" user ""B.Silkscreen"")
		(1 ""F.Mask"" user)
		(3 ""B.Mask"" user)
		(17 ""Dwgs.User"" user ""User.Drawings"")
		(19 ""Cmts.User"" user ""User.Comments"")
		(21 ""Eco1.User"" user ""User.Eco1"")
		(23 ""Eco2.User"" user ""User.Eco2"")
		(25 ""Edge.Cuts"" user)
		(27 ""Margin"" user)
		(31 ""F.CrtYd"" user ""F.Courtyard"")
		(29 ""B.CrtYd"" user ""B.Courtyard"")
		(35 ""F.Fab"" user)
		(33 ""B.Fab"" user)
		(39 ""User.1"" user)
		(41 ""User.2"" user)
		(43 ""User.3"" user)
		(45 ""User.4"" user)
	)
	(setup
".Replace("\r\n", "\n"));
            sb.Append(StackupBlock(r)).Append('\n');
            string yes = r.ViaFill ? "yes" : "no";
            sb.AppendFormat("\t\t(pad_to_mask_clearance 0)\n\t\t(solder_mask_min_width {0})\n\t\t(allow_soldermask_bridges_in_footprints no)\n", S.F(r.MaskBridge));
            sb.Append("\t\t(tenting\n\t\t\t(front yes)\n\t\t\t(back yes)\n\t\t)\n\t\t(covering\n\t\t\t(front no)\n\t\t\t(back no)\n\t\t)\n\t\t(plugging\n\t\t\t(front no)\n\t\t\t(back no)\n\t\t)\n");
            sb.AppendFormat("\t\t(capping {0})\n\t\t(filling {0})\n\t)\n\t(embedded_fonts no)\n)\n", yes);
            return sb.ToString();
        }

        public static string Dru(RuleSet r)
        {
            return "(version 1)\n\n" + string.Join("\n\n", r.Custom) + "\n";
        }

        public static string InfoHtml(IManufacturer m, Selection s, RuleSet r, string name)
        {
            var sb = new StringBuilder();
            sb.Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>").Append(name).Append("</title></head><body style=\"font-family:Segoe UI,Arial,sans-serif;font-size:10pt\">");
            sb.AppendFormat("<h2 style=\"margin:0 0 4px\">{0} &middot; {1}</h2>", m.Name, S.Html(Tx.LayersN(s.Layers)));
            sb.AppendFormat("<p style=\"margin:0 0 8px;color:#555\">{0} &middot; {1} &middot; {2}</p>",
                S.Html(r.Stackup.Name), S.Mm(s.Thickness), S.Html(Tx.CopperSummary(S.Oz(s.Outer), s.Layers > 2 ? S.Oz(s.Inner) : null, S.F(r.Margin))));
            sb.Append("<table cellpadding=\"3\" cellspacing=\"0\" style=\"border-collapse:collapse;font-size:9pt\">");
            string group = null;
            foreach (var row in r.Rows)
            {
                if (row.Group != group) { group = row.Group; sb.AppendFormat("<tr><td colspan=\"3\" style=\"padding-top:8px\"><b>{0}</b></td></tr>", S.Html(group)); }
                sb.AppendFormat("<tr><td>{0}</td><td><b>{1}</b></td><td style=\"color:#666\">{2}</td></tr>", S.Html(row.Name), S.Html(row.Value), S.Html(row.Source));
            }
            sb.Append("</table>");
            foreach (var n in r.Notes) sb.AppendFormat("<p style=\"color:#9a6700\">&#9888; {0}</p>", S.Html(n));
            sb.AppendFormat("<p style=\"color:#555\">{0}</p>", S.Html(Tx.OrderSameOptions(m.Name)));
            sb.Append("<p style=\"color:#999;font-size:8pt\">" + S.Html(Tx.AppName) + " &middot; Aykut Gencer Karaca</p></body></html>");
            return sb.ToString();
        }
    }
}
