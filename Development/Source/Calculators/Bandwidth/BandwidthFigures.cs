using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace KiCadDrc
{
    // Values the live figures of calculator 1 show (set by the calculator on every recalculation)
    class BandwidthState
    {
        public bool Microstrip = true, Metric = true, ByRise = true;
        public double Er = 4.6, ErEff = 2.855, Sr = double.NaN, LIpc = double.NaN, LFreq = double.NaN, Trace = double.NaN, Factor = 0.25;
        public int Divisor = 7;
    }

    static class BandwidthFigures
    {
        // ---- cross-section of the trace: where the electric field runs (air + dielectric for microstrip, dielectric only for stripline)
        public static void CrossSection(Graphics g, Rectangle r, BandwidthState s)
        {
            float w = Math.Min(r.Width * 0.58f, 380), x0 = r.Left + 10, cx = x0 + w / 2;
            float planeH = 9, dielH = s.Microstrip ? 46 : 70, traceW = 46, traceH = 7;
            float bottom = r.Bottom - 12, dielTop = bottom - planeH - dielH;
            // air, dielectric, plane(s)
            // above the trace: air for microstrip; for stripline the board continues above the top plane
            using (var b = new SolidBrush(s.Microstrip ? Fig.Air : Color.FromArgb(120, Fig.Dielectric))) g.FillRectangle(b, x0, r.Top + 6, w, dielTop - r.Top - 6);
            Fig.Box(g, Fig.Dielectric, Fig.DielectricDark, new RectangleF(x0, dielTop, w, dielH));
            Fig.Box(g, Fig.Copper, Fig.CopperDark, new RectangleF(x0, bottom - planeH, w, planeH));
            if (!s.Microstrip) Fig.Box(g, Fig.Copper, Fig.CopperDark, new RectangleF(x0, dielTop - planeH, w, planeH));
            float traceY = s.Microstrip ? dielTop - traceH : dielTop + dielH / 2 - traceH / 2;
            var trace = new RectangleF(cx - traceW / 2, traceY, traceW, traceH);

            // field lines from the trace to the plane(s)
            using (var p = new Pen(Color.FromArgb(170, Fig.FieldLine), 1.3f) { EndCap = LineCap.ArrowAnchor })
            {
                float yPlane = bottom - planeH;
                for (int i = -2; i <= 2; i++)
                {
                    float sx = cx + i * traceW / 5f;
                    float spread = i * 22f;
                    g.DrawBezier(p, sx, trace.Bottom, sx + spread * 0.3f, trace.Bottom + 12, sx + spread, yPlane - 12, sx + spread * 1.2f, yPlane);
                }
                if (s.Microstrip)
                    // the outer lines leave the top of the trace and run through the air
                    foreach (int side in new[] { -1, 1 })
                        for (int k = 0; k < 2; k++)
                        {
                            float sx = cx + side * (traceW / 2 - 4), up = 18 + k * 16, out1 = 30 + k * 26;
                            g.DrawBezier(p, sx, trace.Top, sx + side * 6, trace.Top - up, sx + side * out1, trace.Top - up, sx + side * (out1 + 18), yPlane);
                        }
                else
                {
                    float yTop = dielTop;
                    for (int i = -2; i <= 2; i++)
                    {
                        float sx = cx + i * traceW / 5f, spread = i * 22f;
                        g.DrawBezier(p, sx, trace.Top, sx + spread * 0.3f, trace.Top - 10, sx + spread, yTop + 8, sx + spread * 1.2f, yTop);
                    }
                }
            }
            Fig.Box(g, Fig.Copper, Fig.CopperDark, trace);

            // labels inside the drawing
            if (s.Microstrip) Fig.Text(g, Tx.FigAir, Fig.Small, Fig.Muted, x0 + 6, r.Top + 8);
            Fig.Tag(g, Tx.FigDielectric(double.IsNaN(s.Er) ? "—" : S.F(Math.Round(s.Er, 3))), Fig.Small, Fig.DielectricDark, x0 + 6, dielTop + dielH - 4, ContentAlignment.BottomLeft);
            Fig.Text(g, Tx.FigPlane, Fig.Small, Color.White, x0 + w - 6, bottom - planeH / 2, ContentAlignment.MiddleRight);
            Fig.Tag(g, Tx.FigTrace, Fig.Small, Fig.CopperDark, trace.Right + 4, trace.Top - 1, ContentAlignment.BottomLeft);

            // explanation on the right
            float tx = x0 + w + 16, tw = r.Right - tx - 6;
            if (tw < 80) return;
            Fig.Text(g, s.Microstrip ? "Microstrip" : "Stripline", Fig.Bold, Fig.Ink, tx, r.Top + 10);
            Fig.Text(g, "Er_eff = " + (double.IsNaN(s.ErEff) ? "—" : CalcUi.Sig(s.ErEff, 4)), Fig.Mono, Fig.FieldLine, tx, r.Top + 30);
            var rect = new RectangleF(tx, r.Top + 54, tw, r.Bottom - r.Top - 58);
            using (var b = new SolidBrush(Fig.Muted)) g.DrawString(s.Microstrip ? Tx.FigMicrostripText : Tx.FigStriplineText, Fig.Small, b, rect);
        }

        // ---- the trace to scale: driver, trace, the signal edge spread over Sr, and the two limits.
        // Rows from top to bottom: signal edge, IPC limit label, trace band (zones, trace, chips), wavelength limit label.
        public static void TraceScale(Graphics g, Rectangle r, BandwidthState s)
        {
            double limit = Math.Min(s.LIpc, s.LFreq);
            if (double.IsNaN(limit) || limit <= 0) { Fig.Text(g, "—", Fig.Normal, Fig.Muted, r.Left + r.Width / 2, r.Top + r.Height / 2, ContentAlignment.MiddleCenter); return; }
            bool hasTrace = !double.IsNaN(s.Trace) && s.Trace > 0;
            double maxLen = Math.Max(Math.Max(s.LIpc, s.LFreq), hasTrace ? s.Trace : 0) * 1.12;
            float x0 = r.Left + 52, x1 = r.Right - 20, scale = (float)((x1 - x0) / maxLen);
            Func<double, float> X = m => x0 + (float)(Math.Min(m, maxLen * 1.5) * scale);
            float yEdgeHigh = r.Top + 8, yEdgeLow = r.Top + 34;
            float yTrace = r.Top + r.Height * 0.60f, bandTop = yTrace - 22, bandBottom = yTrace + 22;

            // signal edge: low at the driver, rising over the rise distance Sr (clipped with an arrow when longer than the drawing)
            float srEnd = X(s.Sr);
            bool clipped = srEnd > x1;
            float yAtEnd = clipped ? yEdgeLow - (yEdgeLow - yEdgeHigh) * (x1 - x0) / (srEnd - x0) : yEdgeHigh;
            using (var brush = new SolidBrush(Color.FromArgb(40, Fig.Signal)))
                g.FillPolygon(brush, new[] { new PointF(x0, yEdgeLow), new PointF(Math.Min(srEnd, x1), yAtEnd), new PointF(Math.Min(srEnd, x1), yEdgeLow) });
            using (var p = new Pen(Fig.Signal, 2f))
            {
                g.DrawLine(p, x0, yEdgeLow, Math.Min(srEnd, x1), yAtEnd);
                if (!clipped) g.DrawLine(p, srEnd, yEdgeHigh, x1, yEdgeHigh);
            }
            Fig.Text(g, Tx.FigEdge(CalcUi.LengthShort(s.Sr, s.Metric)) + (clipped ? "  →" : ""), Fig.Small, Fig.Signal, x0 + 4, yEdgeLow + 2);

            // zones along the trace
            using (var b = new SolidBrush(Color.FromArgb(220, 252, 231))) g.FillRectangle(b, x0, bandTop, X(limit) - x0, bandBottom - bandTop);
            using (var b = new SolidBrush(Color.FromArgb(254, 243, 199))) g.FillRectangle(b, X(limit), bandTop, x1 - X(limit), bandBottom - bandTop);
            // The RX chip of an entered trace is placed first: labels inside the band keep clear of it and of both limit
            // markers (their dashed lines run through the whole band). A label that fits nowhere is dropped.
            double traceLen = hasTrace ? s.Trace : limit;
            var rx = new RectangleF(X(traceLen) + 5, yTrace - 11, 26, 22);
            if (rx.Right > r.Right - 2) rx.X = r.Right - 2 - rx.Width;
            var blocked = new List<float[]> { new[] { X(s.LIpc), X(s.LIpc) }, new[] { X(s.LFreq), X(s.LFreq) } };
            if (hasTrace) blocked.Add(new[] { rx.Left - 4, rx.Right + 4 });   // chip with its pins

            // zone names in the lower half of the band, inside their own zone; the careful zone's name prefers its right end
            float safeW = g.MeasureString(Tx.FigSafeZone, Fig.Small).Width, carefulW = g.MeasureString(Tx.FigCarefulZone, Fig.Small).Width;
            float safeX = Fig.FreeSpot(safeW, x0 + 4, X(limit) - 4, blocked, x0 + 4);
            if (!float.IsNaN(safeX)) Fig.Text(g, Tx.FigSafeZone, Fig.Small, Fig.Good, safeX, bandBottom - 1, ContentAlignment.BottomLeft);
            float carefulX = Fig.FreeSpot(carefulW, X(limit) + 4, x1 - 2, blocked, x1 - 2 - carefulW);
            if (!float.IsNaN(carefulX)) Fig.Text(g, Tx.FigCarefulZone, Fig.Small, Fig.Warn, carefulX, bandBottom - 1, ContentAlignment.BottomLeft);

            // trace and chips
            using (var p = new Pen(Fig.Copper, 5f)) g.DrawLine(p, x0, yTrace, X(traceLen), yTrace);
            Fig.Chip(g, new RectangleF(r.Left + 8, yTrace - 14, 34, 28), "TX");
            if (hasTrace)
            {
                Fig.Chip(g, rx, "RX");
                bool ok = s.Trace <= limit;
                string label = (ok ? "✓ " : "⚠ ") + Tx.FigYourTrace(CalcUi.LengthShort(s.Trace, s.Metric));
                // upper half of the band, as close as possible to the left of the RX chip, inside the zone of its verdict
                // (a green ✓ tag never sits in the yellow zone); the tag's white box is 2 px wider on each side
                float lw = g.MeasureString(label, Fig.Bold).Width;
                float lx = Fig.FreeSpot(lw + 4, ok ? x0 : X(limit) + 4, ok ? X(limit) - 4 : x1 + 2, blocked, Math.Min(rx.Left - 6 - lw, x1 - lw) - 2);
                if (!float.IsNaN(lx)) Fig.Tag(g, label, Fig.Bold, ok ? Fig.Good : Fig.Bad, lx + 2, bandTop + 2);
            }

            // limit markers: IPC label above the band, wavelength label below it
            Action<double, string, bool> marker = (len, label, above) =>
            {
                float x = X(len);
                Fig.Dashed(g, Fig.Ink, x, bandTop - 6, x, bandBottom + 6);
                float lw = g.MeasureString(label, Fig.Small).Width;
                float lx = Math.Max(x0 + lw / 2, Math.Min(x, x1 - lw / 2));
                Fig.Tag(g, label, Fig.Small, Fig.Ink, lx, above ? bandTop - 6 : bandBottom + 6, above ? ContentAlignment.BottomCenter : ContentAlignment.TopCenter);
            };
            marker(s.LIpc, "IPC-2251 (" + S.F(s.Factor) + " Sr): " + CalcUi.LengthShort(s.LIpc, s.Metric), true);
            marker(s.LFreq, "λ/" + s.Divisor + ": " + CalcUi.LengthShort(s.LFreq, s.Metric), false);
        }
        // ---- static: a signal edge and its rise time (10 % -> 90 %)
        public static void EdgeWaveform(Graphics g, Rectangle r)
        {
            float x0 = r.Left + 40, x1 = r.Right - 150, yLow = r.Bottom - 26, yHigh = r.Top + 26;
            float t0 = x0 + (x1 - x0) * 0.25f, t1 = x0 + (x1 - x0) * 0.62f;
            using (var p = new Pen(Fig.Muted, 1f)) { g.DrawLine(p, x0, yLow + 8, x1 + 10, yLow + 8); g.DrawLine(p, x0, yLow + 8, x0, yHigh - 6); }
            Fig.Text(g, Tx.FigTime, Fig.Small, Fig.Muted, x1 + 10, yLow + 10, ContentAlignment.TopRight);
            Fig.Text(g, Tx.FigVoltage, Fig.Small, Fig.Muted, x0 - 4, yHigh - 8, ContentAlignment.BottomLeft);
            // edge with rounded corners
            var path = new GraphicsPath();
            path.AddLine(x0, yLow, t0 - 10, yLow);
            path.AddBezier(t0 - 10, yLow, t0, yLow, t0, yLow, t0 + 8, yLow - 6);
            path.AddLine(t0 + 8, yLow - 6, t1 - 8, yHigh + 6);
            path.AddBezier(t1 - 8, yHigh + 6, t1, yHigh, t1, yHigh, t1 + 10, yHigh);
            path.AddLine(t1 + 10, yHigh, x1, yHigh);
            using (var p = new Pen(Fig.Signal, 2.2f)) g.DrawPath(p, path);
            float y10 = yLow - (yLow - yHigh) * 0.1f, y90 = yLow - (yLow - yHigh) * 0.9f;
            float xa = t0 + (t1 - t0) * 0.1f + 3, xb = t0 + (t1 - t0) * 0.9f - 3;
            Fig.Dashed(g, Fig.Muted, x0, y10, x1, y10, 1f); Fig.Dashed(g, Fig.Muted, x0, y90, x1, y90, 1f);
            Fig.Text(g, "10 %", Fig.Small, Fig.Muted, x0 + 2, y10 - 1, ContentAlignment.BottomLeft);
            Fig.Text(g, "90 %", Fig.Small, Fig.Muted, x0 + 2, y90 - 1, ContentAlignment.BottomLeft);
            Fig.Dashed(g, Fig.Ink, xa, y10, xa, yLow + 8, 1f); Fig.Dashed(g, Fig.Ink, xb, y90, xb, yLow + 8, 1f);
            Fig.Arrow(g, Fig.Ink, xa, yLow + 2, xb, yLow + 2, 1.3f, true);
            Fig.Tag(g, "tr", Fig.Bold, Fig.Ink, (xa + xb) / 2, yLow - 2, ContentAlignment.BottomCenter);
            using (var b = new SolidBrush(Fig.Ink)) g.DrawString(Tx.FigEdgeText, Fig.Small, b, new RectangleF(x1 + 22, yHigh, r.Right - x1 - 26, r.Height - 30));
        }

        // ---- static: one wavelength and the lambda/7 piece
        public static void Wavelength(Graphics g, Rectangle r)
        {
            float x0 = r.Left + 20, x1 = r.Right - 20, mid = r.Top + r.Height * 0.45f, amp = r.Height * 0.25f;
            float seg = (x1 - x0) / 7f;
            using (var b = new SolidBrush(Color.FromArgb(220, 252, 231))) g.FillRectangle(b, x0, mid - amp - 6, seg, amp * 2 + 12);
            var pts = new PointF[121];
            for (int i = 0; i <= 120; i++) pts[i] = new PointF(x0 + (x1 - x0) * i / 120f, mid - amp * (float)Math.Sin(2 * Math.PI * i / 120.0));
            using (var p = new Pen(Fig.Signal, 2f)) g.DrawLines(p, pts);
            using (var p = new Pen(Fig.Muted, 1f)) g.DrawLine(p, x0, mid, x1, mid);
            float yb = r.Bottom - 22;
            Fig.Arrow(g, Fig.Ink, x0, yb, x1, yb, 1.3f, true);
            Fig.Tag(g, "λ = c / f", Fig.Bold, Fig.Ink, (x0 + x1) / 2, yb, ContentAlignment.MiddleCenter);
            Fig.Arrow(g, Fig.Good, x0, mid + amp + 12, x0 + seg, mid + amp + 12, 1.3f, true);
            Fig.Tag(g, "λ/7", Fig.Bold, Fig.Good, x0 + seg / 2, mid + amp + 12, ContentAlignment.MiddleCenter);
            Fig.Tag(g, Tx.FigLambdaText, Fig.Small, Fig.Good, x0 + seg + 8, mid - amp - 4, ContentAlignment.TopLeft);
        }
    

        // ---- receiver voltage over time for one or two trace lengths (ReflectionModel), tr = 1 ns, FR-4 microstrip
        public const double DemoRise = 1e-9, DemoSpeed = 177.4e6;   // m/s on FR-4 microstrip (Er_eff 2.855)

        public static void ReceiverWave(Graphics g, Rectangle r, double[] lengthsMm, Color[] colors, string[] names)
        {
            float x0 = r.Left + 44, x1 = r.Right - 12, yTop = r.Top + 14, yBottom = r.Bottom - 30;
            double window = 12e-9, vMax = 5.4, vMin = -0.6;
            Func<double, float> X = tsec => x0 + (float)(tsec / window) * (x1 - x0);
            Func<double, float> Y = v => yBottom - (float)((v - vMin) / (vMax - vMin)) * (yBottom - yTop);
            // axes and grid
            using (var p = new Pen(Color.FromArgb(229, 231, 235), 1f))
                for (int ns = 0; ns <= 12; ns += 2) g.DrawLine(p, X(ns * 1e-9), yTop, X(ns * 1e-9), yBottom);
            using (var p = new Pen(Fig.Muted, 1f)) { g.DrawLine(p, x0, Y(0), x1, Y(0)); g.DrawLine(p, x0, yTop, x0, yBottom); }
            for (int ns = 0; ns <= 12; ns += 2) Fig.Text(g, ns + " ns", Fig.Small, Fig.Muted, X(ns * 1e-9), yBottom + 3, ContentAlignment.TopCenter);
            foreach (double v in new[] { 0.0, 3.3, 5.0 }) Fig.Text(g, S.F(v) + " V", Fig.Small, Fig.Muted, x0 - 4, Y(v), ContentAlignment.MiddleRight);
            // target level and a typical absolute maximum of a 3.3 V input
            Fig.Dashed(g, Fig.Good, x0, Y(3.3), x1, Y(3.3), 1.2f);
            Fig.Tag(g, Tx.FigTargetLevel, Fig.Small, Fig.Good, x1, Y(3.3) + 2, ContentAlignment.TopRight);   // below its line
            Fig.Dashed(g, Fig.Bad, x0, Y(3.6), x1, Y(3.6), 1f);
            Fig.Tag(g, Tx.FigAbsMax, Fig.Small, Fig.Bad, x1, Y(3.6) - 2, ContentAlignment.BottomRight);    // above its line

            for (int c = 0; c < lengthsMm.Length; c++)
            {
                double td = lengthsMm[c] / 1000 / DemoSpeed;
                var pts = new PointF[400];
                double peak = 0, peakT = 0;
                for (int i = 0; i < pts.Length; i++)
                {
                    double tsec = window * i / (pts.Length - 1), v = ReflectionModel.Receiver(tsec, DemoRise, td);
                    pts[i] = new PointF(X(tsec), Y(Math.Max(vMin, Math.Min(vMax, v))));
                    if (v > peak) { peak = v; peakT = tsec; }
                }
                using (var p = new Pen(colors[c], 2.2f)) g.DrawLines(p, pts);
                double over = (peak - 3.3) / 3.3 * 100;
                if (over > 3)
                {
                    using (var b = new SolidBrush(colors[c])) g.FillEllipse(b, X(peakT) - 4, Y(peak) - 4, 8, 8);
                    Fig.Tag(g, "+" + Math.Round(over) + " %", Fig.Bold, colors[c], X(peakT) + 6, Y(peak) - 2, ContentAlignment.BottomLeft);
                }
                if (names != null) Fig.Tag(g, "— " + names[c], Fig.Bold, colors[c], x0 + 8, yTop + c * 16, ContentAlignment.TopLeft);
            }
        }
    }
}
