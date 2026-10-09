using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace KiCadDrc
{
    // ------------------------------------------------------------------ XLSX (Office Open XML, no extra library)
    // Three sheets: summary (project info, order form, cost estimate, part distribution, approval + charts), "BOM", "CPL".
    // Print ready: landscape A4, fit to page width, table header repeated on every page, page numbers in the footer.

    static class Xlsx
    {
        // Style numbers (order of cellXfs in styles.xml)
        const int Header = 1, Body = 2, BodyZ = 3, BigTitle = 4, Note = 5, Num = 6, NumZ = 7, Label = 8, Value = 9, Link = 10, LinkZ = 11,
            Signature = 12, Decimal = 13, DecimalZ = 14, Price4 = 15, Price4Z = 16, Money = 17, MoneyZ = 18, TypeBasic = 19, TypeExtended = 20, TypePreferred = 21,
            StockLow = 22, Thousands = 23, ThousandsZ = 24, Wrap = 25, WrapZ = 26, Section = 27, Faded = 28, LabelNum = 29, LabelMoney = 30, BigMoney = 31;
        const string Author = "Aykut Gencer Karaca";

        static string X(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s ?? "")
            {
                if (c == '&') sb.Append("&amp;"); else if (c == '<') sb.Append("&lt;"); else if (c == '>') sb.Append("&gt;"); else if (c == '"') sb.Append("&quot;");
                else if (c < 0x20 && c != '\t' && c != '\n' && c != '\r') continue;
                else sb.Append(c);
            }
            return sb.ToString();
        }

        static string Col(int i) { string s = ""; for (i++; i > 0; i = (i - 1) / 26) s = (char)('A' + (i - 1) % 26) + s; return s; }

        // Rough number of lines a wrapped cell needs; the row height follows it
        static int LineCount(string text, double width)
        {
            if (string.IsNullOrEmpty(text)) return 1;
            return Math.Max(1, (int)Math.Ceiling(text.Length / Math.Max(4, width - 2)));
        }

        class Sheet
        {
            public StringBuilder Data = new StringBuilder();
            public int Row;
            public List<string> Merged = new List<string>();
            public void NewRow(double height = 0)
            {
                if (Row > 0) Data.Append("</row>");
                Row++;
                Data.Append("<row r=\"" + Row + "\"" + (height > 0 ? " ht=\"" + S.F(height) + "\" customHeight=\"1\"" : "") + ">");
            }
            public void Text(int col, string text, int style)
            {
                Data.Append("<c r=\"" + Col(col) + Row + "\" s=\"" + style + "\" t=\"inlineStr\"><is><t xml:space=\"preserve\">" + X(text) + "</t></is></c>");
            }
            public void Number(int col, double value, int style)
            {
                // 6 digits: floating point residue (214.42000000000002) is not written; coordinates have 4 digits
                Data.Append("<c r=\"" + Col(col) + Row + "\" s=\"" + style + "\"><v>" + Math.Round(value, 6).ToString("R", S.Inv) + "</v></c>");
            }
            public void Formula(int col, string formula, string cached, int style)
            {
                Data.Append("<c r=\"" + Col(col) + Row + "\" s=\"" + style + "\" t=\"str\"><f>" + X(formula) + "</f><v>" + X(cached) + "</v></c>");
            }
            public void NumberFormula(int col, string formula, double cached, int style)
            {
                Data.Append("<c r=\"" + Col(col) + Row + "\" s=\"" + style + "\"><f>" + X(formula) + "</f><v>" + Math.Round(cached, 6).ToString("R", S.Inv) + "</v></c>");
            }
            public string Finish() { return Data.ToString() + (Row > 0 ? "</row>" : ""); }
        }

        static string SheetXml(Sheet s, double[] widths, int freeze, string filter, bool noGrid, bool drawing, string footer)
        {
            var sb = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">");
            sb.Append("<sheetPr><pageSetUpPr fitToPage=\"1\"/></sheetPr>");
            sb.Append("<sheetViews><sheetView workbookViewId=\"0\"" + (noGrid ? " showGridLines=\"0\"" : "") + ">");
            if (freeze > 0)
                sb.Append("<pane ySplit=\"" + freeze + "\" topLeftCell=\"A" + (freeze + 1) + "\" activePane=\"bottomLeft\" state=\"frozen\"/>" +
                    "<selection pane=\"bottomLeft\" activeCell=\"A" + (freeze + 1) + "\" sqref=\"A" + (freeze + 1) + "\"/>");
            sb.Append("</sheetView></sheetViews><sheetFormatPr defaultRowHeight=\"18\"/><cols>");
            for (int i = 0; i < widths.Length; i++) sb.Append("<col min=\"" + (i + 1) + "\" max=\"" + (i + 1) + "\" width=\"" + S.F(widths[i]) + "\" customWidth=\"1\"/>");
            sb.Append("</cols><sheetData>").Append(s.Finish()).Append("</sheetData>");
            if (filter != null) sb.Append("<autoFilter ref=\"" + filter + "\"/>");
            if (s.Merged.Count > 0) sb.Append("<mergeCells count=\"" + s.Merged.Count + "\">" + string.Concat(s.Merged.Select(m => "<mergeCell ref=\"" + m + "\"/>")) + "</mergeCells>");
            sb.Append("<printOptions horizontalCentered=\"1\"/>");
            sb.Append("<pageMargins left=\"0.4\" right=\"0.4\" top=\"0.5\" bottom=\"0.7\" header=\"0.3\" footer=\"0.3\"/>");
            sb.Append("<pageSetup paperSize=\"9\" orientation=\"landscape\" fitToWidth=\"1\" fitToHeight=\"0\"/>");
            // Footer: project on the left, sheet name (&A) in the middle, page n / N on the right. A literal & is written as &&
            sb.Append("<headerFooter><oddFooter>" + X("&L&8" + footer.Replace("&", "&&") + "&C&8&A&R&8" + Tx.XPage + " &P / &N") + "</oddFooter></headerFooter>");
            if (drawing) sb.Append("<drawing r:id=\"rId1\"/>");
            return sb.Append("</worksheet>").ToString();
        }

        // est: quantities, prices and fees as JLC charges them (JlcCost)
        public static void Write(string path, ProjectInfo p, IManufacturer m, Selection s, RuleSet r, List<BomRow> bom, List<CplRow> cpl,
            Dictionary<string, PartInfo> info, int boards, AssemblyEstimate est)
        {
            string date = DateTime.Now.ToString("yyyy-MM-dd HH:mm", S.Inv);
            string footer = p.Name + "  ·  Rev " + p.Revision + "  ·  " + date;
            string summaryName = Tx.XSheetSummary;
            var placed = bom.Where(x => !x.Excluded).ToList();
            Func<BomRow, PartInfo> P = x => { PartInfo pi; return Production.IsValidCode(x.Lcsc) && info.TryGetValue(x.Lcsc, out pi) ? pi : null; };
            // Charged quantity (need + attrition, at least the minimum), its price tier and the line cost come from the estimate
            Func<BomRow, CostLine> line = x => est.Line(x);
            Func<BomRow, double> unit = x => { var l = line(x); return l == null ? double.NaN : l.Unit; };
            Func<BomRow, double> lineTotal = x => { var l = line(x); return l == null ? 0 : l.Total; };
            Func<BomRow, bool> lowStock = x => { var l = line(x); return l != null && l.LowStock; };
            double partsTotal = est.PartsTotal;
            var types = new[] { "Basic", "Preferred", "Extended" };
            Func<string, List<BomRow>> ofType = t => placed.Where(x => P(x) != null && P(x).Type == t).ToList();
            int unpriced = est.Unpriced;
            bool hasInfo = placed.Any(x => P(x) != null);

            // ================================================================ summary
            var sm = new Sheet();
            sm.NewRow(36); sm.Text(0, p.Name + "  ·  " + Tx.XSummaryTitle, BigTitle); sm.Merged.Add("A1:D1");
            sm.NewRow(); sm.Text(0, "Rev " + p.Revision + "  ·  " + date + "  ·  " + Tx.AppName + "  ·  " + Author, Signature); sm.Merged.Add("A2:D2");
            Action<string> section = name => { sm.NewRow(10); sm.NewRow(26); sm.Text(0, name, Section); for (int i = 1; i < 4; i++) sm.Text(i, "", Section); sm.NewRow(6); };
            Action<string, string> infoRow = (l, v) =>
            {
                sm.NewRow(20); sm.Text(0, l, Label); sm.Text(1, v, Value); sm.Text(2, "", Value); sm.Text(3, "", Value);
                sm.Merged.Add("B" + sm.Row + ":D" + sm.Row);
            };
            Action<string, double, int> moneyRow = (l, v, style) =>
            {
                sm.NewRow(style == BigMoney ? 26 : 20); sm.Text(0, l, Label); sm.Number(1, v, style); sm.Text(2, "", style); sm.Text(3, "", style);
                sm.Merged.Add("B" + sm.Row + ":D" + sm.Row);
            };
            Action<string> note = t => { sm.NewRow(); sm.Text(0, t, Note); sm.Merged.Add("A" + sm.Row + ":D" + sm.Row); };
            Action<string[]> tableHeader = names => { sm.NewRow(24); for (int i = 0; i < names.Length; i++) sm.Text(i, names[i], Header); };

            section(Tx.XProjectInfo);
            infoRow(Tx.XProject, p.Name);
            infoRow(Tx.XRevision, p.Revision);
            infoRow(Tx.XDate, date);
            infoRow(Tx.XPreparedBy, Author);
            infoRow(Tx.XBoardCount, Tx.XBoardCountValue(boards));
            infoRow(Tx.XProjectFolder, p.Dir);

            section(Tx.XOrderForm(m.Name));
            Option mask = m.Masks().FirstOrDefault(x => x.Code == s.Mask) ?? m.Masks()[0];
            Option finish = m.Finishes(s.Layers, s.Thickness).FirstOrDefault(y => y.Code == s.Finish) ?? m.Finishes(s.Layers, s.Thickness)[0];
            infoRow(Tx.XLayers, s.Layers.ToString());
            infoRow(Tx.XThickness, S.Mm(s.Thickness));
            infoRow(Tx.XOuterCopper, S.Oz(s.Outer));
            infoRow(Tx.XInnerCopper, s.Layers > 2 ? S.Oz(s.Inner) : "—");
            infoRow("Stackup (Impedance / Layer Stackup)", s.Layers > 2 ? r.Stackup.Name : "—");
            infoRow(Tx.XMaskColor, mask.Name + (mask.Name != mask.KiCad ? " (" + mask.KiCad + ")" : ""));
            infoRow(Tx.XSurfaceFinish, finish.Name);
            infoRow("Via", r.ViaFill ? Tx.XViaFilled : "Tented");
            infoRow(Tx.XMinTrackClearance, S.Mm(Convert.ToDouble(r.Board["min_track_width"])) + " / " + S.Mm(Convert.ToDouble(r.Board["min_clearance"])));
            infoRow(Tx.XMinDrillVia, S.Mm(Convert.ToDouble(r.Board["min_through_hole_diameter"])) + " / " + S.Mm(Convert.ToDouble(r.Board["min_via_diameter"])));
            infoRow(Tx.XGerberPackage, Production.GerberFile(p));
            infoRow(Tx.XBomCpl, Production.BomFile(p) + "  /  " + Production.CplFile(p));

            section(Tx.XCostEstimate(boards));
            if (hasInfo)
            {
                infoRow(Tx.XAssemblyType, est.Standard ? Tx.XStandardBothSides : Tx.XEconomicOneSide);
                moneyRow(Tx.XPartsCost(boards), partsTotal, Money);
                moneyRow(Tx.XPartsPerBoard, partsTotal / boards, Money);
                moneyRow(Tx.XSetupFee(est.Standard, est.Sides), est.Setup, Money);
                moneyRow(Tx.XStencilFee(est.Sides), est.Stencil, Money);
                moneyRow(Tx.XLoadingFee(est.LoadingKinds, S.F(est.LoadingFeePerKind), est.Standard), est.Loading, Money);
                moneyRow(Tx.XSmtJoints(est.SmtJointsPerBoard * boards, S.F(est.SmtRate)), est.SmtCost, Money);
                if (est.ThtJointsPerBoard > 0)
                {
                    moneyRow(Tx.XThtJoints(est.ThtJointsPerBoard * boards, S.F(est.ThtRate)), est.ThtCost, Money);
                    moneyRow(Tx.XHandLabor, est.HandLabor, Money);
                }
                if (est.LeadlessPerBoard > 0) moneyRow(Tx.XXray(est.LeadlessPerBoard * boards, S.F(est.XrayRate)), est.Xray, Money);
                if (est.Packing > 0) moneyRow(Tx.XPacking, est.Packing, Money);
                moneyRow(Tx.XEstimatedTotal, est.Total, BigMoney);
                int low = placed.Count(lowStock);
                sm.NewRow(20); sm.Text(0, Tx.XLowStockRows, Label); sm.Number(1, low, low > 0 ? StockLow : Thousands); sm.Text(2, "", Value); sm.Text(3, "", Value);
                sm.Merged.Add("C" + sm.Row + ":D" + sm.Row);
                if (unpriced > 0) { sm.NewRow(20); sm.Text(0, Tx.XUnpricedRows, Label); sm.Number(1, unpriced, StockLow); sm.Text(2, "", Value); sm.Text(3, "", Value); sm.Merged.Add("C" + sm.Row + ":D" + sm.Row); }
                note(Tx.XCostNote(date.Substring(0, 10), JlcCost.FeeSource));
            }
            else note(Tx.XNoPartInfo);

            section(Tx.XDistribution);
            tableHeader(new[] { Tx.XCategory, Tx.XKinds, Tx.XQtyPerBoard, Tx.XCost(boards) });
            var categories = placed.GroupBy(x => x.Category)
                .Select(g => new { Name = g.Key, Rows = g.Count(), Qty = g.Sum(x => x.Qty), Total = g.Sum(lineTotal) }).OrderByDescending(x => x.Qty).ToList();
            int first = sm.Row + 1; bool zebra = false;
            foreach (var c in categories)
            {
                sm.NewRow(); sm.Text(0, c.Name, zebra ? BodyZ : Body); sm.Number(1, c.Rows, zebra ? NumZ : Num); sm.Number(2, c.Qty, zebra ? NumZ : Num);
                if (hasInfo) sm.Number(3, c.Total, zebra ? MoneyZ : Money); else sm.Text(3, "—", zebra ? NumZ : Num);
                zebra = !zebra;
            }
            int last = sm.Row;
            sm.NewRow(22); sm.Text(0, Tx.XTotalPlaced, Label);
            sm.NumberFormula(1, "SUM(B" + first + ":B" + last + ")", placed.Count, LabelNum);
            sm.NumberFormula(2, "SUM(C" + first + ":C" + last + ")", placed.Sum(x => x.Qty), LabelNum);
            if (hasInfo) sm.NumberFormula(3, "SUM(D" + first + ":D" + last + ")", partsTotal, LabelMoney); else sm.Text(3, "", Label);
            if (bom.Any(x => x.Excluded))
            {
                sm.NewRow(); sm.Text(0, Tx.XNotPlacedRow, Faded); sm.Number(1, bom.Count(x => x.Excluded), Faded); sm.Number(2, bom.Where(x => x.Excluded).Sum(x => x.Qty), Faded); sm.Text(3, "", Faded);
            }

            int typeFirst = 0, typeLast = 0;
            if (hasInfo)
            {
                section(Tx.XPartType);
                tableHeader(new[] { Tx.XType, Tx.XKinds, Tx.XQtyPerBoard, Tx.XAssemblyFee });
                typeFirst = sm.Row + 1;
                foreach (var t in types)
                {
                    var l = ofType(t);
                    sm.NewRow(); sm.Text(0, t, t == "Basic" ? TypeBasic : t == "Preferred" ? TypePreferred : TypeExtended);
                    sm.Number(1, l.Count, Num); sm.Number(2, l.Sum(x => x.Qty), Num);
                    double fee = JlcCost.LoadingFee(t, est.Standard);
                    sm.Text(3, fee > 0 ? Tx.XFeePerKind(S.F(fee)) : t == "Preferred" ? Tx.XNoFeePreferred : Tx.XNoFee, Body);
                }
                typeLast = sm.Row;
            }

            section(Tx.XApproval);
            tableHeader(new[] { Tx.XRole, Tx.XName, Tx.XSignature, Tx.XDate });
            foreach (var g in new[] { new[] { Tx.XPreparedBy, Author, date.Substring(0, 10) }, new[] { Tx.XCheckedBy, "", "" }, new[] { Tx.XApprovedBy, "", "" } })
            {
                sm.NewRow(30); sm.Text(0, g[0], Label); sm.Text(1, g[1], Value); sm.Text(2, "", Value); sm.Text(3, g[2], Value);
            }
            sm.NewRow(10);
            note(Tx.XUploadNote);

            // ================================================================ BOM
            var bs = new Sheet();
            string[] bh = { "#", Tx.XCategory, "Designator", Tx.XQty, Tx.XValue, Tx.XDescription, "Footprint (KiCad)", Tx.XPackageLcsc, Tx.XManufacturer, "MPN",
                "LCSC #", Tx.XJlcType, Tx.XStock, Tx.XCharged(boards), Tx.XUnitPrice, Tx.XTotalBoards(boards), Tx.XAssembly, "Datasheet" };
            double[] bw = { 5, 14, 30, 7, 16, 40, 26, 13, 16, 22, 11, 11, 10, 13, 11, 13, 13, 11 };
            const int TotalCol = 15;   // "Total" column (P); the summary formula below sums it
            string lastCol = Col(bh.Length - 1);
            bs.NewRow(32); bs.Text(0, Tx.XBomTitle + "  ·  " + p.Name, BigTitle); bs.Merged.Add("A1:J1");
            bs.NewRow(); bs.Text(0, "Rev " + p.Revision + "  ·  " + Tx.XBomSubtitle(boards, placed.Count, placed.Sum(x => x.Qty)) + "  ·  " + date + "  ·  " + Author, Signature); bs.Merged.Add("A2:J2");
            bs.NewRow(8);
            bs.NewRow(32);
            for (int i = 0; i < bh.Length; i++) bs.Text(i, bh[i], Header);
            int bomHeader = bs.Row, bomFirst = bs.Row + 1;
            zebra = false; int no = 0;
            foreach (var x in bom.OrderBy(x => x.Excluded).ThenBy(x => x.Category).ThenBy(x => x.Refs))
            {
                var pi = P(x);
                string refs = x.Refs.Replace(",", ", ");
                string description = pi != null ? pi.Description : "";
                int lines = Math.Max(LineCount(refs, bw[2]), LineCount(description, bw[5]));
                bs.NewRow(lines > 1 ? 15 * lines + 4 : 0);
                if (x.Excluded)
                {
                    // Rows that are not placed are faded and not counted in the totals
                    bs.Number(0, ++no, Faded); bs.Text(1, x.Category, Faded); bs.Text(2, refs, Faded); bs.Number(3, x.Qty, Faded); bs.Text(4, x.Value, Faded);
                    bs.Text(5, description, Faded); bs.Text(6, x.Package, Faded); bs.Text(7, pi != null ? pi.Package : "", Faded); bs.Text(8, pi != null ? pi.Manufacturer : "", Faded);
                    bs.Text(9, pi != null ? pi.Mpn : "", Faded); bs.Text(10, Production.IsValidCode(x.Lcsc) ? x.Lcsc : "", Faded);
                    for (int i = 11; i <= TotalCol; i++) bs.Text(i, "", Faded);
                    bs.Text(TotalCol + 1, Tx.XNotPlaced, Faded); bs.Text(TotalCol + 2, "", Faded);
                    continue;
                }
                int g = zebra ? BodyZ : Body, n = zebra ? NumZ : Num, w = zebra ? WrapZ : Wrap;
                bs.Number(0, ++no, n); bs.Text(1, x.Category, g); bs.Text(2, refs, w); bs.Number(3, x.Qty, n);
                bs.Text(4, string.IsNullOrWhiteSpace(x.Value) && pi != null ? pi.Mpn : x.Value, g);
                bs.Text(5, description, w); bs.Text(6, x.Package, g);
                bs.Text(7, pi != null ? pi.Package : "—", g); bs.Text(8, pi != null ? pi.Manufacturer : "—", g); bs.Text(9, pi != null ? pi.Mpn : "—", g);
                bs.Formula(10, "HYPERLINK(\"https://www.lcsc.com/product-detail/" + x.Lcsc + ".html\",\"" + x.Lcsc + "\")", x.Lcsc, zebra ? LinkZ : Link);
                if (pi != null)
                {
                    var cl = line(x);
                    bs.Text(11, pi.Type, pi.Type == "Basic" ? TypeBasic : pi.Type == "Preferred" ? TypePreferred : TypeExtended);
                    bs.Number(12, pi.Stock, lowStock(x) ? StockLow : zebra ? ThousandsZ : Thousands);
                    if (cl != null) bs.Number(13, cl.Charged, zebra ? ThousandsZ : Thousands); else bs.Text(13, "—", n);
                    double f = unit(x);
                    if (double.IsNaN(f)) { bs.Text(14, "—", n); bs.Text(TotalCol, "—", n); }
                    else { bs.Number(14, f, zebra ? Price4Z : Price4); bs.Number(TotalCol, lineTotal(x), zebra ? MoneyZ : Money); }
                }
                else for (int i = 11; i <= TotalCol; i++) bs.Text(i, "—", n);
                bs.Text(TotalCol + 1, Tx.XPlacedByJlc, g);
                if (pi != null && pi.Datasheet.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    bs.Formula(TotalCol + 2, "HYPERLINK(\"" + pi.Datasheet.Replace("\"", "%22") + "\",\"PDF ↗\")", "PDF ↗", zebra ? LinkZ : Link);
                else bs.Text(TotalCol + 2, "", g);
                zebra = !zebra;
            }
            int bomLast = bs.Row;
            bs.NewRow(24);
            for (int i = 0; i < bh.Length; i++) bs.Text(i, "", Label);
            bs.Text(2, Tx.XTotalPlaced, Label);
            bs.Number(3, placed.Sum(x => x.Qty), LabelNum);
            if (hasInfo) bs.NumberFormula(TotalCol, "SUM(" + Col(TotalCol) + bomFirst + ":" + Col(TotalCol) + bomLast + ")", partsTotal, LabelMoney);
            bs.NewRow(8);
            bs.NewRow(); bs.Text(0, Tx.XBomLegend(est.Standard, boards), Note);
            bs.Merged.Add("A" + bs.Row + ":" + lastCol + bs.Row);

            // ================================================================ CPL
            var cs = new Sheet();
            string[] ch = { "Designator", "Mid X (mm)", "Mid Y (mm)", "Layer", "Rotation", Tx.XValue, Tx.XPackage };
            cs.NewRow(32); cs.Text(0, Tx.XCplTitle + "  ·  " + p.Name, BigTitle); cs.Merged.Add("A1:G1");
            cs.NewRow(); cs.Text(0, "Rev " + p.Revision + "  ·  " + Tx.XCplSubtitle(cpl.Count, cpl.Count(x => x.Side == "Top"), cpl.Count(x => x.Side == "Bottom")) + "  ·  " +
                date + "  ·  " + Author, Signature); cs.Merged.Add("A2:G2");
            cs.NewRow(8);
            cs.NewRow(28);
            for (int i = 0; i < ch.Length; i++) cs.Text(i, ch[i], Header);
            int cplHeader = cs.Row;
            zebra = false;
            foreach (var x in cpl)
            {
                cs.NewRow();
                int g = zebra ? BodyZ : Body, n = zebra ? NumZ : Num, d = zebra ? DecimalZ : Decimal;
                cs.Text(0, x.Ref, g); cs.Number(1, x.X, d); cs.Number(2, x.Y, d); cs.Text(3, x.Side, g); cs.Number(4, x.Rotation, n); cs.Text(5, x.Value, g); cs.Text(6, x.Package, g);
                zebra = !zebra;
            }

            // ================================================================ package
            const string Decl = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n";
            const string RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/";
            bool typeChart = typeFirst > 0;
            var parts = new OrderedMap();
            parts.Put("[Content_Types].xml", Decl + "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
                string.Concat(Enumerable.Range(1, 3).Select(i => "<Override PartName=\"/xl/worksheets/sheet" + i + ".xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>")) +
                "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
                "<Override PartName=\"/xl/drawings/drawing1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.drawing+xml\"/>" +
                "<Override PartName=\"/xl/charts/chart1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.drawingml.chart+xml\"/>" +
                (typeChart ? "<Override PartName=\"/xl/charts/chart2.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.drawingml.chart+xml\"/>" : "") +
                "<Override PartName=\"/docProps/core.xml\" ContentType=\"application/vnd.openxmlformats-package.core-properties+xml\"/></Types>");
            parts.Put("_rels/.rels", Decl + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                "<Relationship Id=\"rId1\" Type=\"" + RelNs + "officeDocument\" Target=\"xl/workbook.xml\"/>" +
                "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties\" Target=\"docProps/core.xml\"/></Relationships>");
            parts.Put("docProps/core.xml", Decl + "<cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" " +
                "xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:dcterms=\"http://purl.org/dc/terms/\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">" +
                "<dc:title>" + X(Tx.XDocTitle(p.Name)) + "</dc:title><dc:subject>" + X(Tx.XDocSubject) + "</dc:subject><dc:creator>" + Author + "</dc:creator>" +
                "<cp:keywords>" + X(p.Name + ", Rev " + p.Revision + ", " + m.Name) + "</cp:keywords><cp:lastModifiedBy>" + X(Tx.AppName) + "</cp:lastModifiedBy>" +
                "<dcterms:created xsi:type=\"dcterms:W3CDTF\">" + DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", S.Inv) + "</dcterms:created></cp:coreProperties>");
            parts.Put("xl/workbook.xml", Decl + "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
                "<sheets><sheet name=\"" + X(summaryName) + "\" sheetId=\"1\" r:id=\"rId1\"/><sheet name=\"BOM\" sheetId=\"2\" r:id=\"rId2\"/><sheet name=\"CPL\" sheetId=\"3\" r:id=\"rId3\"/></sheets>" +
                "<definedNames>" +
                "<definedName name=\"_xlnm._FilterDatabase\" localSheetId=\"1\" hidden=\"1\">BOM!$A$" + bomHeader + ":$" + lastCol + "$" + bomLast + "</definedName>" +
                "<definedName name=\"_xlnm._FilterDatabase\" localSheetId=\"2\" hidden=\"1\">CPL!$A$" + cplHeader + ":$G$" + cs.Row + "</definedName>" +
                "<definedName name=\"_xlnm.Print_Titles\" localSheetId=\"1\">BOM!$" + bomHeader + ":$" + bomHeader + "</definedName>" +
                "<definedName name=\"_xlnm.Print_Titles\" localSheetId=\"2\">CPL!$" + cplHeader + ":$" + cplHeader + "</definedName>" +
                "</definedNames></workbook>");
            parts.Put("xl/_rels/workbook.xml.rels", Decl + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                string.Concat(Enumerable.Range(1, 3).Select(i => "<Relationship Id=\"rId" + i + "\" Type=\"" + RelNs + "worksheet\" Target=\"worksheets/sheet" + i + ".xml\"/>")) +
                "<Relationship Id=\"rId4\" Type=\"" + RelNs + "styles\" Target=\"styles.xml\"/></Relationships>");
            parts.Put("xl/styles.xml", Styles());
            parts.Put("xl/worksheets/sheet1.xml", SheetXml(sm, new double[] { 38, 20, 18, 30 }, 0, null, true, true, footer));
            parts.Put("xl/worksheets/sheet2.xml", SheetXml(bs, bw, bomHeader, "A" + bomHeader + ":" + lastCol + bomLast, false, false, footer));
            parts.Put("xl/worksheets/sheet3.xml", SheetXml(cs, new double[] { 13, 12, 12, 9, 10, 26, 30 }, cplHeader, "A" + cplHeader + ":G" + cs.Row, false, false, footer));
            parts.Put("xl/worksheets/_rels/sheet1.xml.rels", Decl + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                "<Relationship Id=\"rId1\" Type=\"" + RelNs + "drawing\" Target=\"../drawings/drawing1.xml\"/></Relationships>");
            // Charts to the right of the summary tables (columns F-M)
            var drawing = new StringBuilder(Decl + "<xdr:wsDr xmlns:xdr=\"http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\">");
            drawing.Append(Anchor(2, "Category chart", 3, 24));
            if (typeChart) drawing.Append(Anchor(3, "Part type chart", 26, 46));
            parts.Put("xl/drawings/drawing1.xml", drawing.Append("</xdr:wsDr>").ToString());
            parts.Put("xl/drawings/_rels/drawing1.xml.rels", Decl + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                "<Relationship Id=\"rId1\" Type=\"" + RelNs + "chart\" Target=\"../charts/chart1.xml\"/>" +
                (typeChart ? "<Relationship Id=\"rId2\" Type=\"" + RelNs + "chart\" Target=\"../charts/chart2.xml\"/>" : "") + "</Relationships>");
            parts.Put("xl/charts/chart1.xml", BarChart(summaryName, Tx.XChartCategory, categories.Select(x => x.Name).ToList(), categories.Select(x => (double)x.Qty).ToList(), first, last));
            if (typeChart)
                parts.Put("xl/charts/chart2.xml", Doughnut(summaryName, Tx.XChartType, types.ToList(), types.Select(t => (double)ofType(t).Count).ToList(), new[] { "16A34A", "0284C7", "EA580C" }, typeFirst, typeLast));

            if (File.Exists(path)) File.Delete(path);
            using (var zip = System.IO.Compression.ZipFile.Open(path, System.IO.Compression.ZipArchiveMode.Create))
                foreach (DictionaryEntry e in parts)
                    using (var w = new StreamWriter(zip.CreateEntry((string)e.Key, System.IO.Compression.CompressionLevel.Optimal).Open(), new UTF8Encoding(false)))
                        w.Write((string)e.Value);
        }

        // Chart frame placed in columns F..M between the given rows on the summary sheet (id: 2 = chart1, 3 = chart2)
        static string Anchor(int id, string name, int topRow, int bottomRow)
        {
            return "<xdr:twoCellAnchor><xdr:from><xdr:col>5</xdr:col><xdr:colOff>0</xdr:colOff><xdr:row>" + topRow + "</xdr:row><xdr:rowOff>0</xdr:rowOff></xdr:from>" +
                "<xdr:to><xdr:col>13</xdr:col><xdr:colOff>0</xdr:colOff><xdr:row>" + bottomRow + "</xdr:row><xdr:rowOff>0</xdr:rowOff></xdr:to>" +
                "<xdr:graphicFrame macro=\"\"><xdr:nvGraphicFramePr><xdr:cNvPr id=\"" + id + "\" name=\"" + X(name) + "\"/><xdr:cNvGraphicFramePr/></xdr:nvGraphicFramePr>" +
                "<xdr:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"0\" cy=\"0\"/></xdr:xfrm><a:graphic><a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/chart\">" +
                "<c:chart xmlns:c=\"http://schemas.openxmlformats.org/drawingml/2006/chart\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\" r:id=\"rId" + (id - 1) + "\"/>" +
                "</a:graphicData></a:graphic></xdr:graphicFrame><xdr:clientData/></xdr:twoCellAnchor>";
        }

        static string Styles()
        {
            string font = "<name val=\"Calibri\"/><family val=\"2\"/>";
            Func<string, string> fill = color => "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FF" + color + "\"/><bgColor indexed=\"64\"/></patternFill></fill>";
            // xf: number format, font, fill, border, alignment (null = none)
            Func<int, int, int, int, string, string> xf = (nf, fontId, fillId, border, align) =>
                "<xf numFmtId=\"" + nf + "\" fontId=\"" + fontId + "\" fillId=\"" + fillId + "\" borderId=\"" + border + "\" xfId=\"0\"" +
                (nf != 0 ? " applyNumberFormat=\"1\"" : "") + (fontId != 0 ? " applyFont=\"1\"" : "") + (fillId != 0 ? " applyFill=\"1\"" : "") +
                (border != 0 ? " applyBorder=\"1\"" : "") + (align != null ? " applyAlignment=\"1\"><alignment " + align + "/></xf>" : "/>");
            const string Center = "horizontal=\"center\" vertical=\"center\"", Middle = "vertical=\"center\"", Right = "horizontal=\"right\" vertical=\"center\"";
            var xfs = new[]
            {
                xf(0, 0, 0, 0, null),                                   // 0
                xf(0, 1, 2, 1, Center + " wrapText=\"1\""),             // 1 header
                xf(0, 0, 0, 1, Middle),                                 // 2 body
                xf(0, 0, 3, 1, Middle),                                 // 3 body zebra
                xf(0, 2, 0, 0, Middle),                                 // 4 big title
                xf(0, 3, 0, 0, Middle),                                 // 5 note
                xf(1, 0, 0, 1, Center),                                 // 6 number
                xf(1, 0, 3, 1, Center),                                 // 7 number zebra
                xf(0, 4, 4, 1, Middle),                                 // 8 label
                xf(0, 0, 0, 1, Middle),                                 // 9 value
                xf(0, 5, 0, 1, Middle),                                 // 10 link
                xf(0, 5, 3, 1, Middle),                                 // 11 link zebra
                xf(0, 6, 0, 0, null),                                   // 12 signature
                xf(166, 0, 0, 1, Center),                               // 13 decimal
                xf(166, 0, 3, 1, Center),                               // 14 decimal zebra
                xf(164, 0, 0, 1, Right),                                // 15 unit price
                xf(164, 0, 3, 1, Right),                                // 16 unit price zebra
                xf(165, 0, 0, 1, Right),                                // 17 money
                xf(165, 0, 3, 1, Right),                                // 18 money zebra
                xf(0, 7, 5, 1, Center),                                 // 19 Basic
                xf(0, 8, 6, 1, Center),                                 // 20 Extended
                xf(0, 9, 7, 1, Center),                                 // 21 Preferred
                xf(3, 10, 8, 1, Center),                                // 22 low stock
                xf(3, 0, 0, 1, Center),                                 // 23 thousands
                xf(3, 0, 3, 1, Center),                                 // 24 thousands zebra
                xf(0, 0, 0, 1, Middle + " wrapText=\"1\""),             // 25 wrapped
                xf(0, 0, 3, 1, Middle + " wrapText=\"1\""),             // 26 wrapped zebra
                xf(0, 12, 0, 2, "vertical=\"bottom\""),                 // 27 section title
                xf(0, 11, 0, 1, Middle + " wrapText=\"1\""),            // 28 faded (not placed)
                xf(3, 4, 4, 1, Center),                                 // 29 label number
                xf(165, 4, 4, 1, Right),                                // 30 label money
                xf(165, 13, 0, 1, Right),                               // 31 big money
            };
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
                "<numFmts count=\"3\"><numFmt numFmtId=\"164\" formatCode=\"&quot;$&quot;#,##0.0000\"/><numFmt numFmtId=\"165\" formatCode=\"&quot;$&quot;#,##0.00\"/>" +
                "<numFmt numFmtId=\"166\" formatCode=\"0.000\"/></numFmts>" +
                "<fonts count=\"14\">" +
                "<font><sz val=\"11\"/><color rgb=\"FF1F2937\"/>" + font + "</font>" +                       // 0 normal
                "<font><b/><sz val=\"11\"/><color rgb=\"FFFFFFFF\"/>" + font + "</font>" +                  // 1 header (white)
                "<font><b/><sz val=\"18\"/><color rgb=\"FF18202E\"/>" + font + "</font>" +                  // 2 big title
                "<font><sz val=\"10\"/><color rgb=\"FF6B7280\"/>" + font + "</font>" +                      // 3 note (grey)
                "<font><b/><sz val=\"11\"/><color rgb=\"FF1F2937\"/>" + font + "</font>" +                  // 4 bold
                "<font><u/><sz val=\"11\"/><color rgb=\"FF2563EB\"/>" + font + "</font>" +                  // 5 link
                "<font><i/><sz val=\"10\"/><color rgb=\"FFB7791F\"/>" + font + "</font>" +                  // 6 signature (gold)
                "<font><b/><sz val=\"10\"/><color rgb=\"FF15803D\"/>" + font + "</font>" +                  // 7 Basic (green)
                "<font><b/><sz val=\"10\"/><color rgb=\"FFC2410C\"/>" + font + "</font>" +                  // 8 Extended (orange)
                "<font><b/><sz val=\"10\"/><color rgb=\"FF0369A1\"/>" + font + "</font>" +                  // 9 Preferred (blue)
                "<font><b/><sz val=\"11\"/><color rgb=\"FFB91C1C\"/>" + font + "</font>" +                  // 10 warning (red)
                "<font><i/><sz val=\"11\"/><color rgb=\"FF9CA3AF\"/>" + font + "</font>" +                  // 11 faded
                "<font><b/><sz val=\"13\"/><color rgb=\"FF1E3A8A\"/>" + font + "</font>" +                  // 12 section title
                "<font><b/><sz val=\"14\"/><color rgb=\"FF1E3A8A\"/>" + font + "</font></fonts>" +          // 13 big amount
                "<fills count=\"9\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill>" +
                fill("1E3A8A") + fill("F3F4F6") + fill("EEF2FF") +                                          // 2 header, 3 zebra, 4 label
                fill("DCFCE7") + fill("FFEDD5") + fill("E0F2FE") + fill("FEE2E2") + "</fills>" +          // 5 green, 6 orange, 7 blue, 8 red
                "<borders count=\"3\"><border><left/><right/><top/><bottom/><diagonal/></border>" +
                "<border><left style=\"thin\"><color rgb=\"FFD1D5DB\"/></left><right style=\"thin\"><color rgb=\"FFD1D5DB\"/></right>" +
                "<top style=\"thin\"><color rgb=\"FFD1D5DB\"/></top><bottom style=\"thin\"><color rgb=\"FFD1D5DB\"/></bottom><diagonal/></border>" +
                "<border><left/><right/><top/><bottom style=\"medium\"><color rgb=\"FF1E3A8A\"/></bottom><diagonal/></border></borders>" +
                "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
                "<cellXfs count=\"" + xfs.Length + "\">" + string.Concat(xfs) + "</cellXfs>" +
                "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles></styleSheet>";
        }

        const string ChartText = "<c:txPr><a:bodyPr/><a:lstStyle/><a:p><a:pPr><a:defRPr sz=\"1000\"><a:solidFill><a:srgbClr val=\"374151\"/></a:solidFill></a:defRPr></a:pPr><a:endParaRPr lang=\"en-US\"/></a:p></c:txPr>";
        const string ChartDecl = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n<c:chartSpace xmlns:c=\"http://schemas.openxmlformats.org/drawingml/2006/chart\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">";
        const string ChartFrame = "<c:spPr><a:solidFill><a:srgbClr val=\"FFFFFF\"/></a:solidFill><a:ln w=\"9525\"><a:solidFill><a:srgbClr val=\"E5E7EB\"/></a:solidFill></a:ln></c:spPr></c:chartSpace>";

        static string ChartTitle(string text)
        {
            return "<c:title><c:tx><c:rich><a:bodyPr/><a:lstStyle/><a:p><a:pPr><a:defRPr sz=\"1300\" b=\"1\"/></a:pPr><a:r><a:rPr lang=\"en-US\" sz=\"1300\" b=\"1\"><a:solidFill><a:srgbClr val=\"18202E\"/></a:solidFill></a:rPr><a:t>" +
                X(text) + "</a:t></a:r></a:p></c:rich></c:tx><c:overlay val=\"0\"/></c:title><c:autoTitleDeleted val=\"0\"/>";
        }

        // Category and value caches + cell references on the summary sheet (A = category, value column given)
        static string Series(string sheet, List<string> names, List<double> values, int first, int last, string valueCol)
        {
            var cat = new StringBuilder("<c:ptCount val=\"" + names.Count + "\"/>");
            var val = new StringBuilder("<c:formatCode>General</c:formatCode><c:ptCount val=\"" + values.Count + "\"/>");
            for (int i = 0; i < names.Count; i++)
            {
                cat.Append("<c:pt idx=\"" + i + "\"><c:v>" + X(names[i]) + "</c:v></c:pt>");
                val.Append("<c:pt idx=\"" + i + "\"><c:v>" + values[i].ToString("R", S.Inv) + "</c:v></c:pt>");
            }
            string sheetRef = "'" + X(sheet.Replace("'", "''")) + "'!";
            return "<c:cat><c:strRef><c:f>" + sheetRef + "$A$" + first + ":$A$" + last + "</c:f><c:strCache>" + cat + "</c:strCache></c:strRef></c:cat>" +
                "<c:val><c:numRef><c:f>" + sheetRef + "$" + valueCol + "$" + first + ":$" + valueCol + "$" + last + "</c:f><c:numCache>" + val + "</c:numCache></c:numRef></c:val>";
        }

        static string BarChart(string sheet, string title, List<string> names, List<double> values, int first, int last)
        {
            return ChartDecl + "<c:roundedCorners val=\"0\"/><c:chart>" + ChartTitle(title) +
                "<c:plotArea><c:layout/>" +
                "<c:barChart><c:barDir val=\"bar\"/><c:grouping val=\"clustered\"/><c:varyColors val=\"0\"/>" +
                "<c:ser><c:idx val=\"0\"/><c:order val=\"0\"/><c:tx><c:v>" + X(Tx.XQty) + "</c:v></c:tx>" +
                "<c:spPr><a:solidFill><a:srgbClr val=\"2563EB\"/></a:solidFill></c:spPr><c:invertIfNegative val=\"0\"/>" +
                "<c:dLbls>" + ChartText + "<c:showLegendKey val=\"0\"/><c:showVal val=\"1\"/><c:showCatName val=\"0\"/><c:showSerName val=\"0\"/><c:showPercent val=\"0\"/><c:showBubbleSize val=\"0\"/></c:dLbls>" +
                Series(sheet, names, values, first, last, "C") + "</c:ser>" +
                "<c:gapWidth val=\"55\"/><c:axId val=\"50010\"/><c:axId val=\"50020\"/></c:barChart>" +
                "<c:catAx><c:axId val=\"50010\"/><c:scaling><c:orientation val=\"maxMin\"/></c:scaling><c:delete val=\"0\"/><c:axPos val=\"l\"/><c:numFmt formatCode=\"General\" sourceLinked=\"0\"/>" +
                "<c:majorTickMark val=\"none\"/><c:minorTickMark val=\"none\"/><c:tickLblPos val=\"nextTo\"/>" + ChartText + "<c:crossAx val=\"50020\"/><c:crosses val=\"autoZero\"/><c:auto val=\"1\"/><c:lblAlgn val=\"ctr\"/><c:lblOffset val=\"100\"/><c:noMultiLvlLbl val=\"0\"/></c:catAx>" +
                "<c:valAx><c:axId val=\"50020\"/><c:scaling><c:orientation val=\"minMax\"/></c:scaling><c:delete val=\"0\"/><c:axPos val=\"t\"/>" +
                "<c:majorGridlines><c:spPr><a:ln w=\"6350\"><a:solidFill><a:srgbClr val=\"E5E7EB\"/></a:solidFill></a:ln></c:spPr></c:majorGridlines>" +
                "<c:numFmt formatCode=\"General\" sourceLinked=\"1\"/><c:majorTickMark val=\"none\"/><c:minorTickMark val=\"none\"/><c:tickLblPos val=\"nextTo\"/>" + ChartText +
                "<c:crossAx val=\"50010\"/><c:crosses val=\"max\"/><c:crossBetween val=\"between\"/></c:valAx>" +
                "</c:plotArea><c:plotVisOnly val=\"1\"/><c:dispBlanksAs val=\"gap\"/></c:chart>" + ChartFrame;
        }

        // Doughnut chart: number of kinds per JLC part type (column B of the table on the summary sheet)
        static string Doughnut(string sheet, string title, List<string> names, List<double> values, string[] colors, int first, int last)
        {
            var slices = new StringBuilder();
            for (int i = 0; i < names.Count; i++)
                slices.Append("<c:dPt><c:idx val=\"" + i + "\"/><c:bubble3D val=\"0\"/><c:spPr><a:solidFill><a:srgbClr val=\"" + colors[i % colors.Length] + "\"/></a:solidFill>" +
                    "<a:ln w=\"19050\"><a:solidFill><a:srgbClr val=\"FFFFFF\"/></a:solidFill></a:ln></c:spPr></c:dPt>");
            return ChartDecl + "<c:roundedCorners val=\"0\"/><c:chart>" + ChartTitle(title) +
                "<c:plotArea><c:layout/><c:doughnutChart><c:varyColors val=\"1\"/>" +
                "<c:ser><c:idx val=\"0\"/><c:order val=\"0\"/><c:tx><c:v>" + X(Tx.XKinds) + "</c:v></c:tx>" + slices +
                "<c:dLbls>" + ChartText + "<c:showLegendKey val=\"0\"/><c:showVal val=\"1\"/><c:showCatName val=\"0\"/><c:showSerName val=\"0\"/><c:showPercent val=\"0\"/><c:showBubbleSize val=\"0\"/><c:showLeaderLines val=\"0\"/></c:dLbls>" +
                Series(sheet, names, values, first, last, "B") + "</c:ser>" +
                "<c:firstSliceAng val=\"0\"/><c:holeSize val=\"55\"/></c:doughnutChart></c:plotArea>" +
                "<c:legend><c:legendPos val=\"r\"/><c:overlay val=\"0\"/>" + ChartText + "</c:legend>" +
                "<c:plotVisOnly val=\"1\"/><c:dispBlanksAs val=\"gap\"/></c:chart>" + ChartFrame;
        }
    }
}
