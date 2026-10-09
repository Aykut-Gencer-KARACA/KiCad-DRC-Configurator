using System;
using System.Collections.Generic;
using System.Linq;

namespace KiCadDrc
{
    // ------------------------------------------------------------------ user-facing text (English / Turkish)
    // Every text the user sees is here; the rest of the code is language independent.
    // L(english, turkish) picks the active language. Values are properties because the language can change at runtime.
    static partial class Tx
    {
        public static bool Turkish;
        static string L(string en, string tr) { return Turkish ? tr : en; }
        // English count with singular/plural noun ("1 row", "3 rows"); Turkish nouns do not change after numbers
        static string N(long n, string one, string many) { return n + " " + (n == 1 ? one : many); }

        // ---------------------------------------------------------------- general
        public static string AppName { get { return L("KiCad DRC Configurator", "KiCad DRC Ayarlayıcı"); } }
        public static string RecommendedMark { get { return L("   ★ recommended", "   ★ önerilen"); } }
        public static string None { get { return L("none", "yok"); } }
        public static string Error { get { return L("Error", "Hata"); } }
        public static string Details { get { return L("Details", "Ayrıntı"); } }
        public static string LayersN(int n) { return L(n + " layers", n + " katman"); }
        public static string White { get { return L("white", "beyaz"); } }
        public static string Black { get { return L("black", "siyah"); } }
    }
}
