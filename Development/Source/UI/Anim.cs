using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace KiCadDrc
{
    // ------------------------------------------------------------------ animation

    static class Anim
    {
        // Calls step(0..1) for the given duration (ease-out), then done
        public static System.Windows.Forms.Timer Play(int duration, Action<double> step, Action done)
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var t = new System.Windows.Forms.Timer { Interval = 15 };
            t.Tick += (o, e) =>
            {
                double x = Math.Min(1.0, clock.ElapsedMilliseconds / (double)duration);
                step(1 - Math.Pow(1 - x, 3));
                if (x < 1) return;
                t.Stop(); t.Dispose();
                if (done != null) done();
            };
            step(0);
            t.Start();
            return t;
        }

        public static System.Windows.Forms.Timer After(int ms, Action action)
        {
            var t = new System.Windows.Forms.Timer { Interval = ms };
            t.Tick += (o, e) => { t.Stop(); t.Dispose(); action(); };
            t.Start();
            return t;
        }

        public static Color Blend(Color a, Color b, double t)
        {
            return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }
    }
}
