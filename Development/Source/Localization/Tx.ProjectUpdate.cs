using System;
using System.Collections.Generic;
using System.Linq;

namespace KiCadDrc
{
    static partial class Tx
    {
        // ---------------------------------------------------------------- project update
        public static string KiCadFileBroken { get { return L("KiCad file is corrupt: unclosed parenthesis.", "KiCad dosyası bozuk: kapanmayan parantez."); } }
        public static string NotAKiCadFile(string name) { return L("Not the expected KiCad file (no " + name + ").", "Beklenen KiCad dosyası değil (" + name + " yok)."); }
        public static string PcbNotFound(string path) { return L("The project's PCB file was not found:\n", "Projenin PCB dosyası bulunamadı:\n") + path; }
        public static string NoCopperLayers(string path) { return L("No copper layer definition found in the PCB file:\n", "PCB dosyasında bakır katman tanımı bulunamadı:\n") + path; }
        public static string LayersNotEmpty(List<Tuple<string, int>> used)
        {
            string list = string.Join("\n", used.Select(x => "•  " + x.Item1 + ":  " + L(N(x.Item2, "item", "items") + " (tracks, vias, zones, drawings...)", x.Item2 + " öğe (iz, via, zone, çizim...)")));
            return L("The layers to be removed contain drawings; nothing was done so that nothing is deleted:\n" + list +
                     "\n\nMove these items to other layers in KiCad or delete them, then try again.",
                     "Kaldırılacak katmanlarda çizim var, hiçbir şey silinmemesi için işlem yapılmadı:\n" + list +
                     "\n\nBu öğeleri KiCad'de başka katmanlara taşıyıp ya da silip tekrar dene.");
        }
        public static string ProjectOpenInKiCad(string locks)
        {
            return L("The project appears to be open in KiCad (" + locks + ").\n\nClose the project in KiCad and try again; if I change it while it is open, " +
                     "KiCad overwrites the changes when it saves.",
                     "Proje KiCad'de açık görünüyor (" + locks + ").\n\nKiCad'de projeyi kapatıp tekrar dene; " +
                     "açıkken değiştirirsem KiCad kaydederken değişikliklerin üstüne yazar.");
        }
        public static string ChangeDruRenewed(string name)
        {
            return L("Custom rules: the app block in " + name + ".kicad_dru was renewed (your rules were kept)",
                     "Özel kurallar: " + name + ".kicad_dru içindeki uygulama bloğu yenilendi (senin kuralların korundu)");
        }
        public static string ChangeDruCreated(string name) { return L("Custom rules: " + name + ".kicad_dru was created", "Özel kurallar: " + name + ".kicad_dru oluşturuldu"); }
        public static string LayerCountAfterUpdate(int actual, int expected)
        {
            return L("Layer count after the update is " + actual + " (expected " + expected + ").", "Güncelleme sonrası katman sayısı " + actual + " (beklenen " + expected + ").");
        }
        public static string ItemCountChanged(string type, int before, int now)
        {
            return L("The number of " + type + " changed after the update (" + before + " → " + now + ").", "Güncelleme sonrası " + type + " sayısı değişti (" + before + " → " + now + ").");
        }
        public static string ChangeLayers(int from, int to, List<string> added, List<string> removed)
        {
            return L("Layers: ", "Katman: ") + from + " → " + to +
                (added.Count > 0 ? L("  (added: ", "  (eklendi: ") + string.Join(", ", added) + ")" : "") +
                (removed.Count > 0 ? L("  (removed: ", "  (kaldırıldı: ") + string.Join(", ", removed) + L(", were empty)", ", boştu)") : "");
        }
        public static string NoSetupSection { get { return L("The PCB file has no (setup) section.", "PCB dosyasında (setup) bölümü yok."); } }
        public static string ChangeStackup(string name, string thickness, string finish) { return "Stackup: " + name + " (" + thickness + L(", finish ", ", yüzey ") + finish + ")"; }
        public static string ChangeMask(string bridge, bool viaFill)
        {
            return L("Mask bridge " + bridge + ", mask expansion 1:1, via fill/cap: " + (viaFill ? "yes" : "no"),
                     "Maske köprüsü " + bridge + ", maske açıklığı 1:1, via dolgu/kapak: " + (viaFill ? "evet" : "hayır"));
        }
        public static string ProUnreadable { get { return L(".kicad_pro could not be read.", ".kicad_pro okunamadı."); } }
        public static string ChangeBoardRules(int changed, string minTrack)
        {
            return L("Board Setup rules: " + changed + " values updated (min track " + minTrack + ")", "Board Setup kuralları: " + changed + " değer güncellendi (min iz " + minTrack + ")");
        }
        public static string ChangePresets(string tracks, string vias) { return L("Preset tracks: " + tracks + "  |  preset vias: " + vias, "Hazır izler: " + tracks + "  |  hazır via'lar: " + vias); }
        public static string ChangeNetClassOk
        {
            get { return L("Default net class: already valid, unchanged (other net classes were not touched)", "Default net class: zaten uygun, değişmedi (diğer net class'lara dokunulmadı)"); }
        }
        public static string ChangeNetClassRaised(string list)
        {
            return "Default net class: " + list + L(" (other net classes were not touched)", " (diğer net class'lara dokunulmadı)");
        }
        public static string DruBlockComment1 { get { return L("This block is written by KiCad DRC Configurator and renewed on every update.", "Bu blok KiCad DRC Ayarlayıcı tarafından yazılır ve her güncellemede yenilenir."); } }
        public static string DruBlockComment2 { get { return L("Add your own rules outside the block (below); for the same constraint yours wins.", "Kendi kurallarını bloğun dışına (aşağıya) ekle; aynı kısıtta seninki geçerli olur."); } }
        public static string DruBlockEndMissing { get { return L("The end of the app block in .kicad_dru was not found; check the file by hand.", ".kicad_dru içindeki uygulama bloğunun sonu bulunamadı; dosyayı elle kontrol et."); } }
    }
}
