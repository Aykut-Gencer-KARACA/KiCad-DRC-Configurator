using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace KiCadDrc
{
    // ------------------------------------------------------------------ figures for calculators
    // A figure is a control that paints itself with a painter function. Live figures call Invalidate() when inputs change.
    class Figure : Control
    {
        readonly Action<Graphics, Rectangle> paint;
        public Figure(int height, Action<Graphics, Rectangle> paint)
        {
            this.paint = paint;
            Height = CalcUi.Px(height);
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Color.White;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            // painters work in 96-dpi units; the transform scales drawing and (pixel-sized) fonts together
            float k = CalcUi.Dpi;
            g.ScaleTransform(k, k);
            try { paint(g, new Rectangle(0, 0, (int)(ClientSize.Width / k), (int)(ClientSize.Height / k))); }
            catch (Exception) { }   // a figure must never break the window (e.g. extreme values while typing)
        }
    }

    // Colours and drawing helpers shared by all figures
    static class Fig
    {
        public static readonly Color Copper = Color.FromArgb(214, 142, 42), CopperDark = Color.FromArgb(160, 98, 20);
        public static readonly Color Dielectric = Color.FromArgb(196, 219, 170), DielectricDark = Color.FromArgb(120, 150, 95);
        public static readonly Color Air = Color.FromArgb(236, 244, 252), FieldLine = Color.FromArgb(37, 99, 235);
        public static readonly Color Ink = Color.FromArgb(31, 41, 55), Muted = Color.FromArgb(107, 114, 128);
        public static readonly Color Good = Color.FromArgb(22, 163, 74), Warn = Color.FromArgb(217, 119, 6), Bad = Color.FromArgb(220, 38, 38);
        public static readonly Color Signal = Color.FromArgb(124, 58, 237);

        // Shared fonts (created once; creating fonts in every paint would leak GDI handles)
        public static readonly Font Small = new Font("Segoe UI", 10.7f, GraphicsUnit.Pixel), Normal = new Font("Segoe UI", 12f, GraphicsUnit.Pixel),
            Bold = new Font("Segoe UI Semibold", 12f, GraphicsUnit.Pixel), Mono = new Font("Consolas", 13.3f, FontStyle.Bold, GraphicsUnit.Pixel);

        public static void Text(Graphics g, string s, Font f, Color c, float x, float y, ContentAlignment align = ContentAlignment.TopLeft)
        {
            var size = g.MeasureString(s, f);
            if (align == ContentAlignment.TopCenter || align == ContentAlignment.MiddleCenter || align == ContentAlignment.BottomCenter) x -= size.Width / 2;
            if (align == ContentAlignment.TopRight || align == ContentAlignment.MiddleRight || align == ContentAlignment.BottomRight) x -= size.Width;
            if (align == ContentAlignment.MiddleLeft || align == ContentAlignment.MiddleCenter || align == ContentAlignment.MiddleRight) y -= size.Height / 2;
            if (align == ContentAlignment.BottomLeft || align == ContentAlignment.BottomCenter || align == ContentAlignment.BottomRight) y -= size.Height;
            using (var b = new SolidBrush(c)) g.DrawString(s, f, b, x, y);
        }

        // Text on a white rounded background so it stays readable over drawings
        public static void Tag(Graphics g, string s, Font f, Color c, float x, float y, ContentAlignment align = ContentAlignment.TopLeft)
        {
            var size = g.MeasureString(s, f);
            float left = x, top = y;
            if (align == ContentAlignment.TopCenter || align == ContentAlignment.MiddleCenter || align == ContentAlignment.BottomCenter) left -= size.Width / 2;
            if (align == ContentAlignment.TopRight || align == ContentAlignment.MiddleRight || align == ContentAlignment.BottomRight) left -= size.Width;
            if (align == ContentAlignment.MiddleLeft || align == ContentAlignment.MiddleCenter || align == ContentAlignment.MiddleRight) top -= size.Height / 2;
            if (align == ContentAlignment.BottomLeft || align == ContentAlignment.BottomCenter || align == ContentAlignment.BottomRight) top -= size.Height;
            using (var b = new SolidBrush(Color.FromArgb(235, Color.White))) g.FillRectangle(b, left - 2, top, size.Width + 4, size.Height);
            using (var b = new SolidBrush(c)) g.DrawString(s, f, b, left, top);
        }

        public static void Arrow(Graphics g, Color c, float x1, float y1, float x2, float y2, float width = 1.5f, bool both = false)
        {
            using (var p = new Pen(c, width))
            {
                p.CustomEndCap = new AdjustableArrowCap(4, 5);
                if (both) p.CustomStartCap = new AdjustableArrowCap(4, 5);
                g.DrawLine(p, x1, y1, x2, y2);
            }
        }

        public static void Dashed(Graphics g, Color c, float x1, float y1, float x2, float y2, float width = 1.2f)
        {
            using (var p = new Pen(c, width) { DashStyle = DashStyle.Dash }) g.DrawLine(p, x1, y1, x2, y2);
        }

        public static void Box(Graphics g, Color fill, Color border, RectangleF r)
        {
            using (var b = new SolidBrush(fill)) g.FillRectangle(b, r);
            using (var p = new Pen(border)) g.DrawRectangle(p, r.X, r.Y, r.Width, r.Height);
        }

        // Free place for a label of width w between lo and hi that keeps a gap to every blocked x range {from, to}
        // (markers, chips). Returns the left x of the spot nearest to the preferred left x, NaN when it fits nowhere
        // (the caller then drops the label rather than overlap).
        public static float FreeSpot(float w, float lo, float hi, IEnumerable<float[]> blocked, float preferred, float gap = 4)
        {
            var free = new List<float[]>();
            float start = lo;
            foreach (var b in blocked.Where(b => b[1] > lo && b[0] < hi).OrderBy(b => b[0]))
            {
                if (b[0] - gap > start) free.Add(new[] { start, b[0] - gap });
                start = Math.Max(start, b[1] + gap);
            }
            if (hi > start) free.Add(new[] { start, hi });
            float best = float.NaN, distance = float.MaxValue;
            foreach (var f in free.Where(f => f[1] - f[0] >= w))
            {
                float x = Math.Max(f[0], Math.Min(preferred, f[1] - w));
                if (Math.Abs(x - preferred) < distance) { distance = Math.Abs(x - preferred); best = x; }
            }
            return best;
        }

        // Small IC package symbol (driver / receiver)
        public static void Chip(Graphics g, RectangleF r, string label)
        {
            using (var b = new SolidBrush(Color.FromArgb(55, 65, 81))) g.FillRectangle(b, r);
            using (var p = new Pen(Color.FromArgb(156, 163, 175), 1.5f))
                for (float y = r.Top + 4; y < r.Bottom - 2; y += 6) { g.DrawLine(p, r.Left - 4, y, r.Left, y); g.DrawLine(p, r.Right, y, r.Right + 4, y); }
            Text(g, label, Small, Color.White, r.Left + r.Width / 2, r.Top + r.Height / 2, ContentAlignment.MiddleCenter);
        }
    }
}
