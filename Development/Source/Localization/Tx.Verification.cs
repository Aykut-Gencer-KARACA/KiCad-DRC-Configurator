using System;
using System.Collections.Generic;
using System.Linq;

namespace KiCadDrc
{
    static partial class Tx
    {
        // ---------------------------------------------------------------- verification
        public static string VerifySummary(int total, int passed) { return L(passed + " of " + total + " checks passed.", total + " kontrolden " + passed + " geçti."); }
        public static string VerifyFailedList { get { return L("Failed:", "Geçmeyenler:"); } }
        public static string VerifyCouldNotRun(string message) { return L("Verification could not run: ", "Doğrulama çalıştırılamadı: ") + message; }
        public static string CheckNpthMin { get { return L("NPTH min hole", "NPTH min delik"); } }
        public static string CheckMaxDrill { get { return L("Max drill", "Max delik"); } }
        public static string CheckBlindVia { get { return L("Blind via forbidden", "Kör via yasağı"); } }
        public static string CheckCastellatedMin { get { return L("Castellated min hole", "Castellated min delik"); } }
        public static string CheckViaHoleTrackInner { get { return L("Via hole to track (inner layer)", "Via deliği - iz (iç katman)"); } }
        public static string CheckNoFalseAlarm { get { return L("No false alarm on compliant pads", "Kurallara uyan pad'lerde yanlış alarm yok"); } }
        public static string CheckPresetVias(string list) { return L("Default and preset vias pass (", "Varsayılan ve hazır via'lar hatasız (") + list + ")"; }
    }
}
