using System;
using System.Collections.Generic;
using System.Linq;

namespace KiCadDrc
{
    static partial class Tx
    {
        // ---------------------------------------------------------------- command line
        public static string CliWarning { get { return L("WARNING: ", "UYARI: "); } }
        public static string CliHelpFooter { get { return L("Options that are not given are filled with the manufacturer's recommendation.", "Verilmeyen seçenekler üreticinin önerisiyle doldurulur."); } }
        public static string CliRestored { get { return L("KiCad is back to its defaults.", "KiCad varsayılana döndü."); } }
        public static string UnknownMaker(string code, string options) { return L("Unknown manufacturer: " + code + ". Options: ", "Bilinmeyen üretici: " + code + ". Seçenekler: ") + options; }
        public static string MustBeNumber(string name, string value) { return L("--" + name + " must be a number (e.g. 1.6): ", "--" + name + " sayı olmalı (örn. 1.6): ") + value; }
        public static string CliImported(int n) { return L("Filled from file: ", "Dosyadan dolan: ") + n; }
        public static string CliConflicts { get { return L("conflicting: ", "çakışan: "); } }
        public static string CliMissing { get { return L("MISSING", "EKSİK"); } }
        public static string CliBomSummary(int rows, int parts, int missing) { return L("Rows: " + rows + " | parts: " + parts + " | missing codes: " + missing, "Satır: " + rows + " | parça: " + parts + " | eksik kod: " + missing); }
        public static string CliCreated { get { return L("Created: ", "Oluşturuldu: "); } }
        public static string CliNote { get { return L("NOTE: ", "NOT: "); } }
        public static string CliProjectLine(string name, int layers, int target, string stackup)
        {
            return L("Project: " + name + "  (" + layers + " layers)  →  " + target + " layers, " + stackup,
                     "Proje: " + name + "  (" + layers + " katman)  →  " + target + " katman, " + stackup);
        }
        public static string CliItems { get { return L("Items: ", "Öğeler: "); } }
        public static string CliInnerUsage { get { return L("Inner layer usage: ", "İç katman kullanımı: "); } }
        public static string CliLock { get { return L("LOCK: ", "KİLİT: "); } }
        public static string CliSelectionFromProject(int layers, string thickness, string copper, string stackup, string mask, string finish)
        {
            return L("Selection read from project: " + layers + " layers, " + thickness + ", " + copper + ", " + stackup + ", mask " + mask + ", finish " + finish,
                     "Projeden okunan seçim: " + layers + " katman, " + thickness + ", " + copper + ", " + stackup + ", maske " + mask + ", yüzey " + finish);
        }
        public static string CliNoSelection { get { return L("No selection from project: ", "Projeden seçim yok: "); } }
        public static string CliBlocked { get { return L("BLOCKED: ", "ENGEL: "); } }
        public static string CliBackup { get { return L("Backup: ", "Yedek: "); } }
        public static string CliApplied { get { return L("Applied: ", "Uygulandı: "); } }
        public static string DrcBeforeAfter(string before, string after) { return L("DRC: before " + before + "  →  after " + after, "DRC: önce " + before + "  →  sonra " + after); }
        public static string DrcBeforeNow(string before, string now) { return L("DRC: before " + before + "  →  now " + now, "DRC: önce " + before + "  →  şimdi " + now); }
        public static string DrcCounts(int errors, int warnings, int unconnected)
        {
            return L(N(errors, "error", "errors") + ", " + N(warnings, "warning", "warnings") + (unconnected > 0 ? ", " + unconnected + " unconnected" : ""),
                     errors + " hata, " + warnings + " uyarı" + (unconnected > 0 ? ", " + unconnected + " bağlantısız" : ""));
        }
    }
}
