using System;
using System.Collections.Generic;
using System.Linq;

namespace KiCadDrc
{
    // Key names and markers used by versions before 2.2 (Turkish). Only needed to read old files.
    static class Legacy
    {
        public const string DruBlockStart = "# --- KiCad DRC Ayarlayıcı: başlangıç", DruBlockEnd = "# --- KiCad DRC Ayarlayıcı: bitiş ---";
        public const string CodesKey = "kodlar", ExcludedKey = "dizilmeyecek";

        static readonly Dictionary<string, string> SettingKeys = new Dictionary<string, string>
        {
            { "kicad_klasoru", KiCadEnvironment.SettingKiCadFolder }, { "son_proje", KiCadEnvironment.SettingLastProject }, { "kart_adedi", KiCadEnvironment.SettingBoardCount }
        };
        static readonly Dictionary<string, string> StateKeys = new Dictionary<string, string>
        {
            { "uretici", "maker" }, { "katman", "layers" }, { "kalinlik", "thickness" }, { "dis", "outer" }, { "ic", "inner" }, { "stackup", "stackup" },
            { "maske", "mask" }, { "yuzey", "finish" }, { "kenar", "edge" }, { "pay", "margin" }, { "sablon", "template" }, { "tarih", "date" },
            { "onceki_sablon_dizini", "previous_template_dir" }
        };
        static readonly Dictionary<string, string> MaskCodes = new Dictionary<string, string>
        {
            { "Yeşil", "green" }, { "Kırmızı", "red" }, { "Sarı", "yellow" }, { "Mavi", "blue" }, { "Mor", "purple" }, { "Siyah", "black" }, { "Beyaz", "white" }
        };

        public static string SettingKey(string k) { string v; return SettingKeys.TryGetValue(k, out v) ? v : k; }
        public static string StateKey(string k) { string v; return StateKeys.TryGetValue(k, out v) ? v : k; }
        public static string MaskCode(string name) { string v; return MaskCodes.TryGetValue(name, out v) ? v : name; }
        public static string EdgeCode(string code) { return code == "freze" ? "routed" : code; }
    }
}
