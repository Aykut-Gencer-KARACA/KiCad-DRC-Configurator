using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace KiCadDrc
{
    partial class MainForm
    {
        // ---------------------------------------------------------- "PCB calculators" tab
        // List of calculators on the left. The selected one shows its title, a one-line summary and two pages:
        // "Calculate" (inputs, live drawings, results) and "How it works" (explanation with figures, formulas, example).
        // Views are built once and kept, so inputs survive switching calculators or tabs.
        public const string SettingCalculator = "calculator", SettingCalcMetric = "calculator_metric";
        Control settingsPanel;
        List<Calculator> calculators;
        ListBox calcList;
        Label calcTitle, calcSummary;
        Panel calcHostPanel;
        RadioButton calcMetric, calcImperial;
        Button calcModeCalc, calcModeGuide;
        bool calcShowGuide;
        readonly CalcHost calcHost = new CalcHost();
        readonly Dictionary<string, Control> calcViews = new Dictionary<string, Control>();
        RuleSet calcRules;   // rules the views were last told about
        TabPage calcPage;

        TabPage CalculatorsTab()
        {
            var page = new TabPage(Tx.TabCalculators) { BackColor = Color.White, Padding = new Padding(8) };
            calculators = CalculatorList.All();
            calcHost.Rules = () => lastRules;
            CalcUi.DetectDpi();
            // cached views that are not on screen are not disposed with the window: release them explicitly
            FormClosed += (o, e) => { foreach (var v in calcViews.Values) if (!v.IsDisposed) v.Dispose(); calcViews.Clear(); };

            calcList = new ListBox { Dock = DockStyle.Left, Width = CalcUi.Px(330), BorderStyle = BorderStyle.None, DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = CalcUi.Px(30), IntegralHeight = false, BackColor = Background };
            // the list gives way to the calculator in small windows
            page.Resize += (o, e) => calcList.Width = Math.Max(CalcUi.Px(200), Math.Min(CalcUi.Px(330), (int)(page.ClientSize.Width * 0.26)));
            foreach (var c in calculators) calcList.Items.Add(c);
            calcList.DrawItem += DrawCalculatorItem;
            calcList.SelectedIndexChanged += (o, e) => OpenCalculator();

            var right = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14, 0, 0, 0) };
            // UseMnemonic = false: "&" in titles is a character, not a shortcut marker
            calcTitle = new Label { Dock = DockStyle.Top, Height = CalcUi.Px(32), Font = new Font("Segoe UI Semibold", 13f), AutoEllipsis = true, UseMnemonic = false };
            calcSummary = new Label { Dock = DockStyle.Top, Height = CalcUi.Px(22), ForeColor = Color.FromArgb(75, 85, 99), AutoEllipsis = true, UseMnemonic = false };

            // bar: page switch on the left, units on the right
            var bar = new Panel { Dock = DockStyle.Top, Height = CalcUi.Px(38), Padding = new Padding(0, 4, 0, 4) };
            calcModeCalc = ModeButton("▦  " + Tx.CalcModeCalculate);
            calcModeGuide = ModeButton("?  " + Tx.CalcModeGuide);
            calcModeGuide.Left = calcModeCalc.Right + CalcUi.Px(6);
            calcModeCalc.Click += (o, e) => { calcShowGuide = false; OpenCalculator(); };
            calcModeGuide.Click += (o, e) => { calcShowGuide = true; OpenCalculator(); };
            var units = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, WrapContents = false };
            calcMetric = new RadioButton { Text = Tx.CalcMetric, AutoSize = true, Margin = new Padding(0, 6, 12, 0) };
            calcImperial = new RadioButton { Text = Tx.CalcImperial, AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
            calcHost.Metric = env.GetSetting(SettingCalcMetric) != "0";
            calcMetric.Checked = calcHost.Metric; calcImperial.Checked = !calcHost.Metric;
            units.Controls.Add(calcMetric); units.Controls.Add(calcImperial);
            calcMetric.CheckedChanged += (o, e) =>
            {
                calcHost.SetMetric(calcMetric.Checked);
                try { env.SetSetting(SettingCalcMetric, calcMetric.Checked ? "1" : "0"); } catch (Exception) { }
            };
            bar.Controls.Add(calcModeCalc); bar.Controls.Add(calcModeGuide); bar.Controls.Add(units);

            calcHostPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 6, 0, 0) };
            var line = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Color.FromArgb(229, 231, 235) };

            // Dock order: added later is placed first
            right.Controls.Add(calcHostPanel); right.Controls.Add(line); right.Controls.Add(bar); right.Controls.Add(calcSummary); right.Controls.Add(calcTitle);
            page.Controls.Add(right); page.Controls.Add(calcList);

            // Views are built when the tab is first shown (the stackup is known by then)
            calcPage = page;
            tabs.SelectedIndexChanged += (o, e) => { if (tabs.SelectedTab == calcPage) OpenCalculator(); };
            string last = env.GetSetting(SettingCalculator);
            calcList.SelectedIndex = Math.Max(0, calculators.FindIndex(c => c.Id == last));
            return page;
        }

        static Button ModeButton(string text)
        {
            var b = new Button { Text = text, FlatStyle = FlatStyle.Flat, Height = CalcUi.Px(30), Width = CalcUi.Px(170), Top = CalcUi.Px(4), Cursor = Cursors.Hand, Font = new Font("Segoe UI", 9.5f) };
            b.FlatAppearance.BorderColor = Color.FromArgb(209, 213, 219);
            return b;
        }

        void DrawCalculatorItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            var c = (Calculator)calcList.Items[e.Index];
            bool selected = (e.State & DrawItemState.Selected) != 0;
            using (var back = new SolidBrush(selected ? Accent : Background)) e.Graphics.FillRectangle(back, e.Bounds);
            var color = selected ? Color.White : c.Ready ? Color.FromArgb(31, 41, 55) : Color.FromArgb(156, 163, 175);
            string text = (e.Index + 1) + ".  " + c.Title;
            TextRenderer.DrawText(e.Graphics, text, c.Ready ? ListFontReady : ListFontPlanned, new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 10, e.Bounds.Height), color,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
        static readonly Font ListFontReady = new Font("Segoe UI Semibold", 9.5f), ListFontPlanned = new Font("Segoe UI", 9.5f);

        void OpenCalculator()
        {
            var c = calcList.SelectedItem as Calculator;
            if (c == null || tabs.SelectedTab != calcPage) return;
            try { env.SetSetting(SettingCalculator, c.Id); } catch (Exception) { }
            // the selection on the left may have changed while another tab was open
            if (!ReferenceEquals(calcRules, lastRules)) { calcRules = lastRules; calcHost.NotifyRules(lastRules); }
            calcTitle.Text = c.Title;
            calcSummary.Text = c.Summary;
            bool guide = calcShowGuide || !c.Ready;
            calcModeCalc.Enabled = c.Ready;
            StyleMode(calcModeCalc, !guide); StyleMode(calcModeGuide, guide);

            string key = c.Id + (guide ? "|guide" : "|calc");
            Control view;
            if (!calcViews.TryGetValue(key, out view))
            {
                view = guide ? c.BuildGuide(calcHost) : c.Build(calcHost);
                view.Dock = DockStyle.Fill;
                calcViews[key] = view;
            }
            if (calcHostPanel.Controls.Count == 1 && calcHostPanel.Controls[0] == view) return;
            calcHostPanel.SuspendLayout();
            calcHostPanel.Controls.Clear();   // views are kept in calcViews (not disposed)
            calcHostPanel.Controls.Add(view);
            calcHostPanel.ResumeLayout();
        }

        static void StyleMode(Button b, bool active)
        {
            b.BackColor = active ? Color.FromArgb(37, 99, 235) : Color.White;
            b.ForeColor = active ? Color.White : Color.FromArgb(31, 41, 55);
            b.FlatAppearance.BorderColor = active ? Color.FromArgb(37, 99, 235) : Color.FromArgb(209, 213, 219);
        }
    }
}
