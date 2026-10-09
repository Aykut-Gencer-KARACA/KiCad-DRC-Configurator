using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace KiCadDrc
{
    // Building blocks for calculator views: two-column layout, groups with label/control rows, number boxes that
    // validate while typing, radio choices, read-only result boxes. Keeps every calculator visually the same.
    static class CalcUi
    {
        public static readonly Color ResultBack = Color.FromArgb(243, 246, 252), ResultText = Color.FromArgb(24, 32, 46);
        public static readonly Color BadBack = Color.FromArgb(254, 226, 226), NoteText = Color.FromArgb(107, 114, 128);
        public static readonly Color GoodText = Color.FromArgb(21, 128, 61), WarnText = Color.FromArgb(180, 83, 9);

        // Screen scaling (1 = 100 %, 1.5 = 150 %). Views are built after the window's automatic scaling, so every fixed
        // pixel size goes through Px().
        public static float Dpi = 1f;
        public static int Px(int value) { return (int)Math.Round(value * Dpi); }
        public static void DetectDpi()
        {
            // KICAD_DRC_TEST_DPI=1.5 simulates 150 % scaling for the layout tests
            float forced;
            if (float.TryParse(Environment.GetEnvironmentVariable("KICAD_DRC_TEST_DPI"), NumberStyles.Float, CultureInfo.InvariantCulture, out forced) && forced > 0) { Dpi = forced; return; }
            try { using (var g = Graphics.FromHwnd(IntPtr.Zero)) Dpi = g.DpiX / 96f; } catch (Exception) { Dpi = 1f; }
        }

        // Two columns side by side (inputs left, results right); each column stacks groups from the top.
        // The whole view scrolls vertically when the window is too short.
        public static Panel Columns(out TableLayoutPanel left, out TableLayoutPanel right)
        {
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            var t = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2, RowCount = 1, Padding = new Padding(0, 4, 0, 0) };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            left = Stack(); right = Stack();
            t.Controls.Add(left, 0, 0); t.Controls.Add(right, 1, 0);
            scroll.Controls.Add(t);
            return scroll;
        }

        static TableLayoutPanel Stack()
        {
            var s = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(0, 0, 8, 0) };
            s.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            return s;
        }

        // Titled group holding label/control rows; returns the grid to add rows to
        public static TableLayoutPanel Group(TableLayoutPanel column, string title)
        {
            var g = new GroupBox { Text = title, Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(8, 6, 8, 6),
                Margin = new Padding(0, 0, 0, 10), Font = new Font("Segoe UI Semibold", 9.5f) };
            var grid = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Font = new Font("Segoe UI", 9.5f) };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45)); grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            g.Controls.Add(grid);
            column.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            column.Controls.Add(g, 0, column.RowCount++);
            return grid;
        }

        public static Label Row(TableLayoutPanel grid, string label, Control control)
        {
            var l = new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 7, 6, 3), ForeColor = Color.FromArgb(55, 65, 81) };
            control.Dock = DockStyle.Fill; control.Margin = new Padding(0, 3, 0, 3);
            grid.Controls.Add(l); grid.Controls.Add(control);
            return l;
        }

        // Full-width note below the rows of a group
        public static Label Note(TableLayoutPanel grid, string text)
        {
            var l = new Label { Text = text, AutoSize = true, ForeColor = NoteText, Margin = new Padding(0, 4, 0, 2), Font = new Font("Segoe UI", 8.5f) };
            grid.Controls.Add(l); grid.SetColumnSpan(l, 2);
            grid.SizeChanged += (o, e) => l.MaximumSize = new Size(Math.Max(100, grid.ClientSize.Width - 4), 0);
            return l;
        }

        // Number box: accepts "1.5" and "1,5"; turns red while the text is not a valid number in [min, max]
        public static TextBox Number(string initial, double min, double max, Action changed)
        {
            var t = new TextBox { Text = initial };
            t.TextChanged += (o, e) => { t.BackColor = double.IsNaN(Value(t, min, max)) ? BadBack : SystemColors.Window; changed(); };
            t.Tag = new[] { min, max };
            return t;
        }

        public static double Value(TextBox t)
        {
            var range = t.Tag as double[];
            return range == null ? Parse(t.Text) : Value(t, range[0], range[1]);
        }

        static double Value(TextBox t, double min, double max)
        {
            double v = Parse(t.Text);
            return double.IsNaN(v) || v < min || v > max ? double.NaN : v;
        }

        public static double Parse(string s)
        {
            double v;
            return double.TryParse((s ?? "").Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : double.NaN;
        }

        public static ComboBox Choice(IEnumerable<object> items, int selected, Action changed)
        {
            var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.System };
            foreach (var i in items) c.Items.Add(i);
            c.SelectedIndex = Math.Min(selected, c.Items.Count - 1);
            c.SelectedIndexChanged += (o, e) => changed();
            return c;
        }

        // Radio buttons side by side; returns the buttons in order
        public static RadioButton[] Radios(TableLayoutPanel grid, string label, string[] options, int selected, Action changed)
        {
            var panel = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Margin = new Padding(0) };
            var buttons = options.Select((o, i) => new RadioButton { Text = o, AutoSize = true, Checked = i == selected, Margin = new Padding(0, 3, 14, 0) }).ToArray();
            foreach (var b in buttons) { panel.Controls.Add(b); b.CheckedChanged += (o, e) => { if (((RadioButton)o).Checked) changed(); }; }
            Row(grid, label, panel);
            return buttons;
        }

        public static TextBox Result(TableLayoutPanel grid, string label)
        {
            var t = new TextBox { ReadOnly = true, BackColor = ResultBack, ForeColor = ResultText, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Consolas", 10f, FontStyle.Bold), TabStop = false };
            Row(grid, label, t);
            return t;
        }

        // Result box whose label can be changed later (e.g. when the input mode changes)
        public static Label ResultRow(TableLayoutPanel grid, string label, out TextBox box)
        {
            box = new TextBox { ReadOnly = true, BackColor = ResultBack, ForeColor = ResultText, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Consolas", 10f, FontStyle.Bold), TabStop = false };
            return Row(grid, label, box);
        }

        // Drawing across both columns of a group
        public static Figure Figure(TableLayoutPanel grid, int height, Action<Graphics, Rectangle> paint)
        {
            var f = new Figure(height, paint) { Dock = DockStyle.Fill, Margin = new Padding(0, 6, 0, 4) };
            grid.Controls.Add(f); grid.SetColumnSpan(f, 2);
            return f;
        }

        // Number box with its unit selector on the right
        public static Control WithUnit(TextBox box, ComboBox unit)
        {
            var p = new TableLayoutPanel { ColumnCount = 2, Height = Px(28), Margin = new Padding(0) };
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Px(70)));
            box.Dock = DockStyle.Fill; box.Margin = new Padding(0, 0, 4, 0);
            unit.Dock = DockStyle.Fill; unit.Margin = new Padding(0);
            p.Controls.Add(box, 0, 0); p.Controls.Add(unit, 1, 0);
            return p;
        }

        // ---- formatting

        // n significant digits, no exponent for normal magnitudes
        public static string Sig(double x, int digits = 5)
        {
            if (double.IsNaN(x) || double.IsInfinity(x)) return "—";
            if (x == 0) return "0";
            // extreme magnitudes: scientific notation instead of "0" or 16-digit numbers
            if (Math.Abs(x) >= 1e15 || Math.Abs(x) < 1e-9) return x.ToString("0." + new string('#', Math.Max(0, digits - 1)) + "E+0", CultureInfo.InvariantCulture);
            int decimals = Math.Max(0, digits - 1 - (int)Math.Floor(Math.Log10(Math.Abs(x))));
            return Math.Round(x, Math.Min(decimals, 12)).ToString("0." + new string('#', Math.Min(decimals, 12)), CultureInfo.InvariantCulture);
        }

        // Length in the selected system, the other one in parentheses
        public static string Length(double metres, bool metric)
        {
            if (double.IsNaN(metres)) return "—";
            string mm = metres >= 1 ? Sig(metres) + " m" : Sig(metres * 1000) + " mm";
            string inch = Sig(metres / 0.0254) + " in";
            return metric ? mm + "  (" + inch + ")" : inch + "  (" + mm + ")";
        }

        // Length in the selected system only (for drawings)
        public static string LengthShort(double metres, bool metric)
        {
            if (double.IsNaN(metres)) return "—";
            if (!metric) return Sig(metres / 0.0254, 4) + " in";
            return metres >= 1 ? Sig(metres, 4) + " m" : Sig(metres * 1000, 4) + " mm";
        }

        public static string Frequency(double hz)
        {
            if (double.IsNaN(hz)) return "—";
            if (hz >= 1e9) return Sig(hz / 1e9) + " GHz";
            if (hz >= 1e6) return Sig(hz / 1e6) + " MHz";
            if (hz >= 1e3) return Sig(hz / 1e3) + " kHz";
            return Sig(hz) + " Hz";
        }

        public static string Time(double s)
        {
            if (double.IsNaN(s)) return "—";
            if (s >= 1) return Sig(s) + " s";
            if (s >= 1e-3) return Sig(s * 1e3) + " ms";
            if (s >= 1e-6) return Sig(s * 1e6) + " µs";
            if (s >= 1e-9) return Sig(s * 1e9) + " ns";
            return Sig(s * 1e12) + " ps";
        }
    }
}
