using System;

namespace KiCadDrc
{
    // Bandwidth & maximum conductor length (when does a trace become a transmission line).
    // All inputs and outputs in SI units: seconds, hertz, metres, metres per second.
    //
    // IPC-2251 method (rise-time based):
    //   rise distance      Sr   = tr * v                        (distance the edge travels during the rise time)
    //   propagation speed  v    = c / sqrt(Er_eff)
    //   stripline          Er_eff = Er                          (conductor fully embedded in the dielectric)
    //   microstrip         Er_eff = 0.475 * Er + 0.67           (Motorola MECL System Design Handbook / IPC closed form;
    //                                                            part of the field runs in air above the trace)
    //   max length         L    = k * Sr, k = 0.2 ... 0.5       (0.25 is the usual rule of thumb)
    // Frequency domain method:
    //   bandwidth / knee   f    = 0.35 / tr                     (first-order system, 10-90 % rise time)
    //   wavelength in air  lambda = c / f
    //   max length         L    = lambda / n, n = 4, 7, 10, 20  (1/7 by default)
    static class BandwidthMath
    {
        public const double C = 299792458.0;                 // speed of light in vacuum, m/s
        public const double MicrostripA = 0.475, MicrostripB = 0.67;
        // Saturn PCB Toolkit 8.47 uses 0.457 * Er + 0.67 (only used by the comparison test)
        public const double SaturnMicrostripA = 0.457;

        public static double BandwidthFromRise(double tr) { return 0.35 / tr; }
        public static double RiseFromFrequency(double f) { return 0.35 / f; }

        public static double ErEffective(double er, bool microstrip) { return ErEffective(er, microstrip, MicrostripA); }
        public static double ErEffective(double er, bool microstrip, double a) { return microstrip ? a * er + MicrostripB : er; }

        public static double Speed(double erEff) { return C / Math.Sqrt(erEff); }
        public static double DelayPerMetre(double erEff) { return Math.Sqrt(erEff) / C; }
        public static double RiseDistance(double tr, double erEff) { return tr * Speed(erEff); }
        public static double MaxLengthIpc(double tr, double erEff, double factor) { return factor * RiseDistance(tr, erEff); }

        public static double WavelengthAir(double f) { return C / f; }
        public static double WavelengthIn(double f, double erEff) { return Speed(erEff) / f; }
        public static double MaxLengthFrequency(double f, double divisor) { return WavelengthAir(f) / divisor; }

        public static readonly double[] SrFactors = { 0.20, 0.25, 0.30, 0.35, 0.40, 0.45, 0.50 };
        public static readonly int[] LambdaDivisors = { 4, 7, 10, 20 };
    }
}
