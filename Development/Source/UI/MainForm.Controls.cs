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
        // --- control helpers

        static ComboBox Combo(TableLayoutPanel t, string label)
        {
            FieldLabel(t, label);
            var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, Margin = new Padding(0, 4, 0, 4), FlatStyle = FlatStyle.System };
            t.Controls.Add(c);
            return c;
        }

        static void FieldLabel(TableLayoutPanel t, string text)
        {
            t.Controls.Add(new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = Color.FromArgb(55, 65, 81), Margin = new Padding(0, 8, 6, 4) });
        }

        static ListView MakeList(string[] headers, int[] widths)
        {
            var lv = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, BorderStyle = BorderStyle.None, HeaderStyle = ColumnHeaderStyle.Nonclickable };
            // no flicker during the flash animation
            typeof(Control).GetProperty("DoubleBuffered", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(lv, true, null);
            for (int i = 0; i < headers.Length; i++) lv.Columns.Add(headers[i], widths[i]);
            lv.ClientSizeChanged += (o, e) =>
            {
                int used = 0; for (int i = 0; i < lv.Columns.Count - 1; i++) used += lv.Columns[i].Width;
                lv.Columns[lv.Columns.Count - 1].Width = Math.Max(120, lv.ClientSize.Width - used - 4);
            };
            return lv;
        }

        static Button MakeButton(string text, bool primary)
        {
            var b = new Button { Text = text, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = new Font("Segoe UI", 9.5f, primary ? FontStyle.Bold : FontStyle.Regular) };
            b.FlatAppearance.BorderColor = primary ? Accent : Color.FromArgb(209, 213, 219);
            b.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(29, 78, 216) : Color.FromArgb(243, 244, 246);
            b.BackColor = primary ? Accent : Color.White;
            b.ForeColor = primary ? Color.White : Color.FromArgb(31, 41, 55);
            return b;
        }

        static Control Spacer(DockStyle d) { return new Panel { Dock = d, Width = 8 }; }

        static Icon AppIcon()
        {
            try { return Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { return null; }
        }
    }
}
