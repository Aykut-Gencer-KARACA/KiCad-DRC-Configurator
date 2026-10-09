using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace KiCadDrc
{
    partial class MainForm
    {
        IManufacturer Maker { get { return manufacturers[Math.Max(0, cMaker.SelectedIndex)]; } }

        void Start(Selection s)
        {
            if (s == null)
            {
                var d = env.State();
                s = d != null ? Selection.FromMap(d) : manufacturers[0].Recommended(2, double.NaN, double.NaN, double.NaN);
            }
            loading = true;
            cMaker.SelectedIndex = Math.Max(0, manufacturers.FindIndex(m => m.Code == s.Maker));
            nMargin.Value = (decimal)Math.Min(0.15, Math.Max(0, s.Margin));
            loading = false;
            Fill(s);
            WriteStatus();
        }

        void LayersChanged()
        {
            var now = Collect();
            var s = Maker.Recommended(LayerCount, double.NaN, double.NaN, double.NaN);
            s.Mask = now.Mask; s.Edge = now.Edge; s.Margin = now.Margin;
            Fill(s);
        }

        void SelectRecommended()
        {
            var s = Maker.Recommended(LayerCount, double.NaN, double.NaN, double.NaN);
            loading = true; nMargin.Value = (decimal)s.Margin; loading = false;
            Fill(s);
        }

        void ShowHint(string field)
        {
            lHint.Text = "ⓘ  " + Maker.Hint(field);
            lHint.Visible = true;
        }

        // Refills the lower combos from the upper choices; keeps the previous choice if valid, else the recommendation
        void Fill() { Fill(Collect()); }

        void Fill(Selection s)
        {
            loading = true;
            var m = Maker;
            const double None = double.NaN;
            Choose(cLayers, m.LayerCounts().Select(x => (object)new Option(x.ToString(), Tx.LayersN(x), "", x)).ToArray(), s.Layers.ToString(), null);
            int L = LayerCount;
            var r0 = m.Recommended(L, None, None, None);
            Choose(cThickness, m.Thicknesses(L).Select(x => (object)new Option(S.F(x), S.Mm(x), "", x) { Recommended = x == r0.Thickness }).ToArray(), S.F(s.Thickness), S.F(r0.Thickness));
            double th = ValueOf(cThickness);
            var r1 = m.Recommended(L, th, None, None);
            Choose(cOuter, m.OuterCoppers(L, th).Select(x => (object)new Option(S.F(x), S.Oz(x), "", x) { Recommended = x == r1.Outer }).ToArray(), S.F(s.Outer), S.F(r1.Outer));
            double outer = ValueOf(cOuter);
            var r2 = m.Recommended(L, th, outer, None);
            var inner = m.InnerCoppers(L, th, outer);
            Choose(cInner, inner.Length == 0 ? new object[] { new Option("0", "—", "", 0) } : inner.Select(x => (object)new Option(S.F(x), S.Oz(x), "", x) { Recommended = x == r2.Inner }).ToArray(), S.F(s.Inner), S.F(r2.Inner));
            cInner.Enabled = inner.Length > 0;
            var r3 = m.Recommended(L, th, outer, ValueOf(cInner));
            var st = m.Stackups(L, th, outer, ValueOf(cInner));
            cStackup.Items.Clear(); foreach (var x in st) cStackup.Items.Add(x);
            int si = st.FindIndex(x => x.Name == s.Stackup);
            if (si < 0) si = st.FindIndex(x => x.Name == r3.Stackup);
            cStackup.SelectedIndex = st.Count == 0 ? -1 : Math.Max(0, si);
            cStackup.Enabled = st.Count > 1;
            var masks = m.Masks(); foreach (var x in masks) x.Recommended = x.Code == r3.Mask;
            Choose(cMask, masks, s.Mask, r3.Mask);
            var finishes = m.Finishes(L, th); foreach (var y in finishes) y.Recommended = y.Code == r3.Finish;
            Choose(cFinish, finishes, s.Finish, r3.Finish);
            var edges = m.Edges(); foreach (var e in edges) e.Recommended = e.Code == r3.Edge;
            Choose(cEdge, edges, s.Edge, r3.Edge);
            loading = false;
            Recalculate();
        }

        void Choose(ComboBox c, object[] items, string code, string fallbackCode)
        {
            c.Items.Clear(); c.Items.AddRange(items);
            int i = Array.FindIndex(items, o => ((Option)o).Code == code);
            if (i < 0 && fallbackCode != null) i = Array.FindIndex(items, o => ((Option)o).Code == fallbackCode);
            c.SelectedIndex = items.Length == 0 ? -1 : Math.Max(0, i);
        }

        int LayerCount { get { return (int)ValueOf(cLayers); } }
        static double ValueOf(ComboBox c) { return c.SelectedItem == null ? 0 : ((Option)c.SelectedItem).Value; }
        static string CodeOf(ComboBox c) { return c.SelectedItem == null ? null : ((Option)c.SelectedItem).Code; }
        static string NameOf(ComboBox c) { return c.SelectedItem == null ? "" : ((Option)c.SelectedItem).Name; }

        Selection Collect()
        {
            return new Selection
            {
                Maker = Maker.Code, Layers = LayerCount, Thickness = ValueOf(cThickness), Outer = ValueOf(cOuter), Inner = ValueOf(cInner),
                Stackup = cStackup.SelectedItem == null ? null : ((Stackup)cStackup.SelectedItem).Name,
                Mask = CodeOf(cMask), Finish = CodeOf(cFinish), Edge = CodeOf(cEdge), Margin = (double)nMargin.Value
            };
        }

        void Recalculate()
        {
            try { lastRules = Maker.Calculate(Collect()); }
            catch (Exception e)
            {
                lastRules = null; bApply.Enabled = false; bApply.BackColor = bApply.FlatAppearance.BorderColor = Disabled;
                lNote.Text = "⚠  " + e.Message; lNote.Visible = true; return;
            }
            if (buttonTimer == null && !busy) { bApply.Enabled = true; bApply.BackColor = bApply.FlatAppearance.BorderColor = Accent; bApply.Text = Tx.Apply; }

            // Choices that differ from the manufacturer's recommendation
            var sel = Collect();
            var rec = Maker.Recommended(sel.Layers, double.NaN, double.NaN, double.NaN);
            var diffs = new List<string>();
            if (sel.Thickness != rec.Thickness) diffs.Add(Tx.DiffThickness + " " + S.Mm(sel.Thickness));
            if (sel.Outer != rec.Outer) diffs.Add(Tx.DiffOuter + " " + S.Oz(sel.Outer));
            if (sel.Layers > 2 && sel.Inner != rec.Inner) diffs.Add(Tx.DiffInner + " " + S.Oz(sel.Inner));
            if (sel.Stackup != rec.Stackup && sel.Thickness == rec.Thickness && sel.Outer == rec.Outer && sel.Inner == rec.Inner) diffs.Add("stackup " + sel.Stackup);
            if (sel.Mask != rec.Mask) diffs.Add(Tx.DiffMask + " " + NameOf(cMask).ToLower());
            if (sel.Finish != rec.Finish) diffs.Add(Tx.DiffFinish + " " + NameOf(cFinish));
            if (sel.Edge != rec.Edge) diffs.Add(Tx.DiffEdge + " " + (sel.Edge == "routed" ? Tx.RoutedShort : "V-cut"));
            if (Math.Abs(sel.Margin - rec.Margin) > 1e-9) diffs.Add(Tx.DiffMargin + " " + S.Mm(sel.Margin));
            lDiff.Text = diffs.Count == 0 ? "★  " + Tx.AllRecommended(Maker.Name) : Tx.DifferentFromRecommended + "  " + string.Join(", ", diffs);
            lDiff.ForeColor = diffs.Count == 0 ? Color.FromArgb(21, 128, 61) : Color.FromArgb(180, 83, 9);

            // Rules that changed flash yellow briefly
            var old = previousValues; previousValues = new Dictionary<string, string>();
            var changed = new List<ListViewItem>();
            lvRules.BeginUpdate(); lvRules.Items.Clear(); lvRules.Groups.Clear();
            var groups = new Dictionary<string, ListViewGroup>();
            foreach (var r in lastRules.Rows)
            {
                ListViewGroup g;
                if (!groups.TryGetValue(r.Group, out g)) { g = new ListViewGroup(r.Group); groups[r.Group] = g; lvRules.Groups.Add(g); }
                var item = new ListViewItem(new[] { r.Name, r.Value, r.Source }, g);
                lvRules.Items.Add(item);
                string key = r.Group + "|" + r.Name;
                previousValues[key] = r.Value;
                string before;
                if (old != null && (!old.TryGetValue(key, out before) || before != r.Value)) changed.Add(item);
            }
            lvRules.EndUpdate();
            FlashItems(changed);

            lvStackup.BeginUpdate(); lvStackup.Items.Clear();
            var st = lastRules.Stackup; int b = 0;
            lvStackup.Items.Add(new ListViewItem(new[] { "F.Mask", Tx.MaskWord, "0.01", "3.8", cMask.Text }));
            foreach (var layer in st.Layers)
            {
                string name = layer.IsCopper ? (b == 0 ? "F.Cu" : b == st.L - 1 ? "B.Cu" : "In" + b + ".Cu") : "";
                var item = layer.IsCopper
                    ? new ListViewItem(new[] { name, Tx.CopperWord, S.F(layer.Thickness), "", S.Oz(Math.Round(layer.Thickness / 0.035 * 2) / 2) })
                    : new ListViewItem(new[] { "", layer.Type, S.F(layer.Thickness), S.F(layer.Dk), layer.Material });
                if (layer.IsCopper) { item.BackColor = CopperColor; b++; }
                lvStackup.Items.Add(item);
            }
            lvStackup.Items.Add(new ListViewItem(new[] { "B.Mask", Tx.MaskWord, "0.01", "3.8", cMask.Text }));
            lvStackup.EndUpdate();
            if (previousStackup != null && previousStackup != st.Name) FlashItems(lvStackup.Items.Cast<ListViewItem>().ToList());
            previousStackup = st.Name;
            lStackupTotal.Text = "  " + st.Name + "  ·  " + Tx.StackupTotals(S.Mm(st.Total), S.Mm(ValueOf(cThickness)));

            lNote.Text = string.Join("\n\n", lastRules.Notes.Select(n => "⚠  " + n));
            lNote.Visible = lastRules.Notes.Count > 0;
        }

        void WriteStatus()
        {
            var d = env.State();
            if (d == null) { lStatus.Text = Tx.StatusDefault(env.Version, env.InstallDir); return; }
            var s = Selection.FromMap(d);
            var m = manufacturers.FirstOrDefault(x => x.Code == s.Maker);
            lStatus.Text = Tx.StatusApplied(m == null ? s.Maker : m.Name, s.Layers, S.Mm(s.Thickness), S.Oz(s.Outer), s.Layers > 2 ? S.Oz(s.Inner) : null, s.Stackup,
                d.ContainsKey("template") ? Convert.ToString(d["template"]) : "", d.ContainsKey("date") ? Convert.ToString(d["date"]) : "");
        }
    }
}
