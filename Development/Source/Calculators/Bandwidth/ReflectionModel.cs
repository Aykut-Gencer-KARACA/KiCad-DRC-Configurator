using System;

namespace KiCadDrc
{
    // What the receiver sees at the end of an unterminated trace (lossless transmission line, "bounce diagram" model).
    // Used by the "How it works" page to SHOW why long traces ring; not used for the calculator results.
    //
    //   driver: ideal source vs with output resistance Rs (CMOS output ~ 10-25 ohm), edge = linear ramp (10-90 % = tr)
    //   trace:  impedance Z0, one-way delay Td = length / v
    //   receiver: CMOS input = open end (reflection +1)
    //   wave launched into the trace:  Vi(t) = vs * Z0 / (Rs + Z0) * ramp(t)
    //   receiver voltage:  V(t) = (1 + GL) * sum_k (GL * GS)^k * Vi(t - (2k + 1) Td),  GL = 1,  GS = (Rs - Z0) / (Rs + Z0)
    // When the round trip 2 Td is short compared with tr the reflections overlap with the edge and cancel out (clean edge);
    // when it is long they arrive one by one as steps above and below the final level (overshoot, ringing).
    static class ReflectionModel
    {
        public const double Rs = 15, Z0 = 50, Vs = 3.3;

        // Linear edge whose 10-90 % time is tr (full swing in tr / 0.8)
        static double Ramp(double t, double tr)
        {
            double full = tr / 0.8;
            if (t <= 0) return 0;
            return t >= full ? 1 : t / full;
        }

        public static double Receiver(double t, double tr, double td)
        {
            double gs = (Rs - Z0) / (Rs + Z0), gl = 1.0;
            double launch = Vs * Z0 / (Rs + Z0);
            double v = 0, k = 1;
            for (int i = 0; i < 400; i++)
            {
                double tt = t - (2 * i + 1) * td;
                if (tt <= 0) break;
                v += (1 + gl) * k * launch * Ramp(tt, tr);
                k *= gl * gs;
                if (Math.Abs(k) < 1e-6) break;
            }
            return v;
        }

        // Largest overshoot above the final level (Vs) in percent, over the first few round trips
        public static double OvershootPercent(double tr, double td)
        {
            double end = tr / 0.8 + 12 * td + tr, peak = 0;
            for (int i = 0; i <= 2000; i++) peak = Math.Max(peak, Receiver(end * i / 2000, tr, td));
            return Math.Max(0, (peak - Vs) / Vs * 100);
        }
    }
}
