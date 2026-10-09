using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace KiCadDrc
{
    // "How it works" page of a calculator: headings, paragraphs, bullet lists, formula boxes, figures with captions and a
    // worked example, stacked vertically and wrapped to the page width.
    class CalcDoc
    {
        readonly TableLayoutPanel body;
        public readonly Panel Root;

        public CalcDoc()
        {
            Root = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White };
            body = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, Padding = new Padding(4, 4, 18, 16) };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Root.Controls.Add(body);
            // wrapped labels follow the page width
            Root.Resize += (o, e) => Wrap();
        }

        // Every label on the page wraps to the page width; labels inside boxes, Q&A blocks and the glossary are reached
        // recursively (their indent is subtracted). A label with Tag "fixed" keeps its size.
        void Wrap()
        {
            int w = Math.Max(200, Root.ClientSize.Width - body.Padding.Horizontal - 26);
            foreach (Control c in body.Controls) WrapIn(c, w - c.Margin.Horizontal);
        }

        static void WrapIn(Control c, int width)
        {
            var l = c as Label;
            if (l != null) { if (l.Tag as string != "fixed") l.MaximumSize = new Size(Math.Max(80, width), 0); return; }
            var term = c.Tag as string == "terms" ? c as TableLayoutPanel : null;
            foreach (Control x in c.Controls)
            {
                int inner = width - c.Padding.Horizontal - x.Margin.Horizontal;
                if (term != null) inner = term.GetColumn(x) == 0 ? TermWidth : width - TermWidth - 24;
                WrapIn(x, inner);
            }
        }
        const int TermWidth = 150;

        void Add(Control c) { body.RowStyles.Add(new RowStyle(SizeType.AutoSize)); body.Controls.Add(c, 0, body.RowCount++); Wrap(); }

        static Label L(string text, Font font, Color color, Padding margin)
        {
            return new Label { Text = text, AutoSize = true, Font = font, ForeColor = color, Margin = margin, UseMnemonic = false };
        }

        public CalcDoc Heading(string text)
        {
            Add(L(text, new Font("Segoe UI Semibold", 11.5f), Color.FromArgb(30, 58, 138), new Padding(0, 14, 0, 4)));
            return this;
        }

        public CalcDoc Para(string text)
        {
            Add(L(text, new Font("Segoe UI", 9.75f), Color.FromArgb(31, 41, 55), new Padding(0, 2, 0, 6)));
            return this;
        }

        public CalcDoc Bullets(params string[] items)
        {
            foreach (var i in items) Add(L("•   " + i, new Font("Segoe UI", 9.75f), Color.FromArgb(31, 41, 55), new Padding(14, 1, 0, 3)));
            return this;
        }

        // Numbered step with a short bold title
        public CalcDoc Step(int n, string title, string text)
        {
            Add(L(n + ".  " + title, new Font("Segoe UI Semibold", 10f), Color.FromArgb(31, 41, 55), new Padding(0, 8, 0, 2)));
            Add(L(text, new Font("Segoe UI", 9.75f), Color.FromArgb(55, 65, 81), new Padding(22, 0, 0, 4)));
            return this;
        }

        // Formula in a shaded box, one formula per line, with an optional explanation below each
        public CalcDoc Formula(params string[] lines)
        {
            var box = new Panel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Color.FromArgb(243, 244, 246), Padding = new Padding(12, 8, 12, 8),
                Margin = new Padding(14, 4, 0, 8), Tag = "box" };
            var stack = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, BackColor = Color.Transparent, Dock = DockStyle.Fill };
            foreach (var line in lines)
                stack.Controls.Add(L(line, line.StartsWith("   ") ? new Font("Segoe UI", 8.75f) : new Font("Consolas", 10.5f, FontStyle.Bold),
                    line.StartsWith("   ") ? Color.FromArgb(107, 114, 128) : Color.FromArgb(24, 32, 46), new Padding(0, 1, 0, 1)));
            box.Controls.Add(stack);
            Add(box);
            return this;
        }

        // Highlighted box (worked example, practical advice)
        public CalcDoc Callout(string title, string text, Color back, Color fore)
        {
            var box = new Panel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = back, Padding = new Padding(12, 8, 12, 10), Margin = new Padding(0, 8, 0, 8), Tag = "box" };
            var stack = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, BackColor = Color.Transparent, Dock = DockStyle.Fill };
            stack.Controls.Add(L(title, new Font("Segoe UI Semibold", 10f), fore, new Padding(0, 0, 0, 4)));
            stack.Controls.Add(L(text, new Font("Segoe UI", 9.75f), fore, new Padding(0)));
            box.Controls.Add(stack);
            Add(box);
            return this;
        }

        // A control embedded in the page (e.g. an interactive demo)
        public CalcDoc Embed(Control c)
        {
            c.Dock = DockStyle.Fill; c.Margin = new Padding(14, 6, 0, 8);
            Add(c);
            return this;
        }

        // Question in bold, answer below it (FAQ)
        public CalcDoc Qa(string question, string answer)
        {
            Add(L("?  " + question, new Font("Segoe UI Semibold", 10f), Color.FromArgb(31, 41, 55), new Padding(0, 8, 0, 2)));
            Add(L(answer, new Font("Segoe UI", 9.75f), Color.FromArgb(55, 65, 81), new Padding(22, 0, 0, 4)));
            return this;
        }

        // Glossary: term on the left, explanation on the right
        public CalcDoc Terms(params string[][] rows)
        {
            var table = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(14, 4, 0, 8), Tag = "terms" };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, TermWidth + 8)); table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            foreach (var r in rows)
            {
                table.Controls.Add(L(r[0], new Font("Segoe UI Semibold", 9.5f), Color.FromArgb(30, 58, 138), new Padding(0, 3, 8, 3)));
                table.Controls.Add(L(r[1], new Font("Segoe UI", 9.5f), Color.FromArgb(55, 65, 81), new Padding(0, 3, 0, 3)));
            }
            Add(table);
            return this;
        }

        public CalcDoc Picture(Control figure, string caption)
        {
            figure.Dock = DockStyle.Fill; figure.Margin = new Padding(14, 6, 0, 0);
            Add(figure);
            if (caption != null) Add(L(caption, new Font("Segoe UI", 8.75f, FontStyle.Italic), Color.FromArgb(107, 114, 128), new Padding(14, 2, 0, 10)));
            return this;
        }
    }
}
