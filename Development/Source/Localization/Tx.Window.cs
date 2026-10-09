using System;
using System.Collections.Generic;
using System.Linq;

namespace KiCadDrc
{
    static partial class Tx
    {
        // ---------------------------------------------------------------- main window
        public static string MoreWarnings(int n) { return L("(+" + N(n, "warning", "warnings") + ", click)", "(+" + n + " uyarı, tıkla)"); }
        public static string Apply { get { return L("Apply", "Uygula"); } }
        public static string Close { get { return L("Close", "Kapat"); } }
        public static string RestoreDefaults { get { return L("Restore defaults", "Varsayılana dön"); } }
        public static string HowToUse { get { return L("How to use?", "Nasıl kullanılır?"); } }
        public static string DataFolderLink { get { return L("Data folder", "Veri klasörü"); } }
        public static string Manufacturer { get { return L("Manufacturer", "Üretici"); } }
        public static string Layers { get { return L("Layers", "Katman"); } }
        public static string Thickness { get { return L("Thickness", "Kalınlık"); } }
        public static string OuterCopper { get { return L("Outer copper", "Dış bakır"); } }
        public static string InnerCopper { get { return L("Inner copper", "İç bakır"); } }
        public static string MaskColor { get { return L("Mask colour", "Maske rengi"); } }
        public static string Finish { get { return L("Finish", "Yüzey"); } }
        public static string BoardEdge { get { return L("Board edge", "Kart kenarı"); } }
        public static string SafetyMargin { get { return L("Safety margin", "Güvenlik payı"); } }
        public static string SafetyMarginNote { get { return L("mm, added to the manufacturer's limits (max 0.15)", "mm, üreticinin sınırlarına eklenir (en fazla 0.15)"); } }
        public static string SelectRecommended { get { return L("Select recommended", "Önerilenleri seç"); } }
        public static string ApplyToProject { get { return L("Apply to existing project…", "Mevcut projeye uygula…"); } }
        public static string TabRules { get { return L("Rules to apply", "Uygulanacak kurallar"); } }
        public static string ColRule { get { return L("Rule", "Kural"); } }
        public static string ColValue { get { return L("Value", "Değer"); } }
        public static string ColSource { get { return L("Source", "Kaynak"); } }
        public static string ColLayer { get { return L("Layer", "Katman"); } }
        public static string ColType { get { return L("Type", "Tip"); } }
        public static string ColThickness { get { return L("Thickness", "Kalınlık"); } }
        public static string ColMaterial { get { return L("Material", "Malzeme"); } }
        public static string MaskWord { get { return L("mask", "maske"); } }
        public static string CopperWord { get { return L("copper", "bakır"); } }
        public static string StackupTotals(string total, string order)
        {
            return L("calculated thickness " + total + " (without mask)  ·  order thickness " + order, "hesaplanan kalınlık " + total + " (maske hariç)  ·  sipariş kalınlığı " + order);
        }
        public static string WaitForOperation { get { return L("Wait for the running operation to finish, then change the language.", "Dili değiştirmek için süren işlemin bitmesini bekle."); } }
        public static string StatusDefault(string version, string dir)
        {
            return L("Current: KiCad defaults  ·  KiCad " + version + " (" + dir + ")", "Şu an: KiCad varsayılanı  ·  KiCad " + version + " (" + dir + ")");
        }
        public static string StatusApplied(string maker, int layers, string thickness, string outer, string inner, string stackup, string template, string date)
        {
            return L("Current: " + maker + " " + layers + " layers  ·  " + thickness + "  ·  outer " + outer + (inner != null ? " / inner " + inner : "") +
                     "  ·  " + stackup + "  ·  template " + template + "  ·  " + date,
                     "Şu an: " + maker + " " + layers + " katman  ·  " + thickness + "  ·  dış " + outer + (inner != null ? " / iç " + inner : "") +
                     "  ·  " + stackup + "  ·  şablon " + template + "  ·  " + date);
        }
        public static string DiffThickness { get { return L("thickness", "kalınlık"); } }
        public static string DiffOuter { get { return L("outer copper", "dış bakır"); } }
        public static string DiffInner { get { return L("inner copper", "iç bakır"); } }
        public static string DiffMask { get { return L("mask", "maske"); } }
        public static string DiffFinish { get { return L("finish", "yüzey"); } }
        public static string DiffEdge { get { return L("edge", "kenar"); } }
        public static string DiffMargin { get { return L("margin", "pay"); } }
        public static string AllRecommended(string maker) { return L("All choices are as " + maker + " recommends", "Seçimlerin hepsi " + maker + "'nin önerdiği gibi"); }
        public static string DifferentFromRecommended { get { return L("Different from recommended:", "Önerilenden farklı:"); } }
        public static string Applying { get { return L("Applying…", "Uygulanıyor…"); } }
        public static string Verifying { get { return L("Verifying", "Doğrulanıyor"); } }
        public static string AppliedVerifying { get { return L("Applied, verifying with KiCad…", "Uygulandı, KiCad ile doğrulanıyor…"); } }
        public static string Applied { get { return L("Applied", "Uygulandı"); } }
        public static string CheckIt { get { return L("Check", "Kontrol et"); } }
        public static string AppliedReopen(string template)
        {
            return L("✓  Applied.  Restart KiCad  →  File › New Project from Template › User Templates › " + template,
                     "✓  Uygulandı.  KiCad'i kapatıp aç  →  File › New Project from Template › User Templates › " + template);
        }
        public static string AppliedAndVerified(int passed, string template)
        {
            return L("Applied and verified with KiCad (" + passed + "/" + passed + " checks).  Restart KiCad  →  File › New Project from Template › User Templates › " + template,
                     "Uygulandı ve KiCad ile doğrulandı (" + passed + "/" + passed + " kontrol).  KiCad'i kapatıp aç  →  File › New Project from Template › User Templates › " + template);
        }
        public static string PassedChecks { get { return L("Passed checks:", "Geçen kontroller:"); } }
        public static string AppliedNotVerified { get { return L("Applied but could not be verified with KiCad. Click for details.", "Uygulandı ama KiCad ile doğrulanamadı. Ayrıntı için tıkla."); } }
        public static string VerificationFailed(int n)
        {
            return L(N(n, "check", "checks") + " failed in verification. Do not order with these settings; click for details.", "Doğrulamada " + n + " kontrol geçmedi. Bu ayarlarla sipariş verme; ayrıntı için tıkla.");
        }
        public static string RestoreConfirm
        {
            get
            {
                return L("The generated template will be deleted and KiCad's template folder setting will be restored. Your existing projects are not affected.\n\nContinue?",
                         "Oluşturulan şablon silinecek ve KiCad'in şablon klasörü ayarı orijinal haline dönecek. Mevcut projelerin etkilenmez.\n\nDevam edilsin mi?");
            }
        }
        public static string Restored { get { return L("KiCad is back to its default settings.  Existing projects were not affected.", "KiCad varsayılan ayarlarına döndü.  Mevcut projeler etkilenmedi."); } }
        public static string ClickForMore { get { return L("(click)", "(tıkla)"); } }
        public static string ClickForDetails { get { return L("click for details", "ayrıntı için tıkla"); } }

        // project update from the window
        public static string SelectProjectToUpdate { get { return L("Select the KiCad project to apply the settings to", "Ayarların uygulanacağı KiCad projesini seç"); } }
        public static string KiCadProjectFilter { get { return L("KiCad project (*.kicad_pro)|*.kicad_pro", "KiCad projesi (*.kicad_pro)|*.kicad_pro"); } }
        public static string ProjectOpenWarning(string name)
        {
            return L(name + " appears to be open in KiCad.\n\nClose the project in KiCad and try again; if I change it while it is open, KiCad overwrites the changes when it saves.",
                     name + " KiCad'de açık görünüyor.\n\nKiCad'de projeyi kapatıp tekrar dene; açıkken değiştirirsem KiCad kaydederken değişikliklerin üstüne yazar.");
        }
        public static string ProjectOpenTitle { get { return L("Project is open", "Proje açık"); } }
        public static string CannotReduceLayers { get { return L("Layers cannot be reduced", "Katman azaltılamaz"); } }
        public static string ApplyToProjectConfirm(string name, string dir, UpdatePlan plan, string stackup, string thickness, string rules, string margin)
        {
            string layers = plan.From == plan.To ? plan.From + L(" (unchanged)", " (değişmiyor)") : plan.From + " → " + plan.To +
                (plan.Added.Count > 0 ? L("\n      to add: ", "\n      eklenecek: ") + string.Join(", ", plan.Added) +
                    L("  (above B.Cu; review your zones for the new plan)", "  (B.Cu'nun üstüne; zone'larını yeni plana göre gözden geçir)") : "") +
                (plan.Removed.Count > 0 ? L("\n      to remove: ", "\n      kaldırılacak: ") + string.Join(", ", plan.Removed) + L("  (empty)", "  (boş)") : "");
            return L("Project:  ", "Proje:  ") + name + "\n" + dir + "\n\n" +
                   L("Layers:  ", "Katman:  ") + layers + "\n" +
                   "Stackup:  " + stackup + "  (" + thickness + ")\n" +
                   L("Rules:  ", "Kurallar:  ") + rules + L(", margin +", ", pay +") + margin + "\n\n" +
                   L("Your drawings, layer names, net classes and your own custom rules are not touched; a backup is taken first.\n" +
                     "KiCad DRC runs before and after, and the error counts are compared.\n\nContinue?",
                     "Çizimlerine, katman adlarına, net class'larına ve kendi özel kurallarına dokunulmaz; önce yedek alınır.\n" +
                     "İşlemden önce ve sonra KiCad ile DRC çalıştırılıp hata sayıları karşılaştırılır.\n\nDevam edilsin mi?");
        }
        public static string UpdatingProject { get { return L("Updating project", "Proje güncelleniyor"); } }
        public static string UpdatingProjectBanner(string name)
        {
            return L(name + " is being updated; KiCad DRC runs before and after…", name + " güncelleniyor; önce ve sonra KiCad ile DRC çalıştırılıyor…");
        }
        public static string ProjectNotUpdated(string name)
        {
            return L(name + " could not be updated; the project was not changed. Click for details.", name + " güncellenemedi; proje değiştirilmedi. Ayrıntı için tıkla.");
        }
        public static string ProjectUpdated(string name, string layers) { return L(name + " updated (" + layers + ").", name + " güncellendi (" + layers + ")."); }
        public static string NewErrorsFound(int n) { return L("the new rules found " + n + " new errors, click", "yeni kurallar " + n + " yeni hata buldu, tıkla"); }
        public static string ChangesHeader { get { return L("Changes:", "Yapılanlar:"); } }
        public static string DrcNote { get { return L("DRC note", "DRC notu"); } }
        public static string BackupHeader { get { return L("Backup (previous state):", "Yedek (eski hali):"); } }
        public static string OpenAndRunDrc
        {
            get
            {
                return L("Open the project in KiCad and check the errors reported by the new rules with Inspect › Design Rules Checker.",
                         "KiCad'de projeyi açıp Inspect › Design Rules Checker ile yeni kurallara göre çıkan hatalara bak.");
            }
        }

        // production tab
        public static string TabProduction { get { return L("Project and production", "Proje ve üretim"); } }
        public static string NoProjectSelected { get { return L("No project selected", "Proje seçilmedi"); } }
        public static string SelectProjectHint { get { return L("Choose your KiCad project (.kicad_pro) with \"Select project…\" on the right.", "Sağdaki \"Proje seç…\" ile KiCad projeni (.kicad_pro) seç."); } }
        public static string SelectProject { get { return L("Select project…", "Proje seç…"); } }
        public static string SelectKiCadProject { get { return L("Select the KiCad project", "KiCad projesini seç"); } }
        public static string Refresh { get { return L("Refresh", "Yenile"); } }
        public static string ColPlace { get { return L("Place", "Dizilsin"); } }
        public static string ColCategory { get { return L("Category", "Kategori"); } }
        public static string ColPartValue { get { return L("Value", "Değer"); } }
        public static string ColQty { get { return L("Qty", "Adet"); } }
        public static string BomEmptyHint
        {
            get
            {
                return L("When a project is selected, every part in the schematic is listed here.\nLCSC codes are read from the symbols' \"LCSC Part\" field (KiCad: select the part, press E); " +
                         "enter the missing ones here. When all are complete the production files can be built.",
                         "Proje seçince şemadaki bütün parçalar burada listelenir.\nLCSC kodları sembollerin \"LCSC Part\" alanından okunur (KiCad'de parçayı seç, E'ye bas); " +
                         "eksik olanları buraya gir. Hepsi tamamlanınca üretim dosyaları oluşturulabilir.");
            }
        }
        public static string CodeFromSchematic
        {
            get
            {
                return L("From the schematic (field \"LCSC Part\"). To change it, edit the symbol in KiCad (select it, press E), save, then Refresh.",
                         "Şemadan (\"LCSC Part\" alanı). Değiştirmek için KiCad'de sembolü düzenle (seç, E'ye bas), kaydet, sonra Yenile'ye bas.");
            }
        }
        public static string RowsFromSchematic(int n) { return L(N(n, "row", "rows") + " from the schematic", n + " satır şemadan"); }
        public static string ImportCodes { get { return L("Import codes from file…", "Kodları dosyadan al…"); } }
        public static string BuildFiles { get { return L("Build production files", "Üretim dosyalarını oluştur"); } }
        public static string BoardCount { get { return L("Boards", "Kart adedi"); } }
        public static string OpenFolder { get { return L("Open folder", "Klasörü aç"); } }
        public static string SettingsTakenFromProject(string summary) { return L("Left panel settings taken from the project: ", "Soldaki ayarlar projeden alındı: ") + summary; }
        public static string ReadingBom { get { return L("Reading the part list from the schematic…", "Şemadan parça listesi okunuyor…"); } }
        public static string EmptyValue { get { return L("(empty value)", "(değer boş)"); } }
        public static string RowsNotPlaced(int n) { return L(N(n, "row", "rows") + " not placed", n + " satır dizilmeyecek"); }
        public static string AllCodesComplete(int n) { return L(n == 1 ? "The LCSC code of the only row is complete" : "LCSC codes of all " + n + " rows are complete", n + " satırın hepsinin LCSC kodu tamam"); }
        public static string CodesMissingShort(int n) { return L(N(n, "row has", "rows have") + " a missing or invalid LCSC code (e.g. C14663)", n + " satırın LCSC kodu eksik ya da geçersiz (örn. C14663)"); }
        public static string ImportCodesTitle { get { return L("Select the file containing the LCSC codes (table, list)", "LCSC kodlarının yazdığı dosyayı seç (tablo, liste)"); } }
        public static string ImportCodesFilter { get { return L("Part list (*.md;*.csv;*.txt)|*.md;*.csv;*.txt|All files (*.*)|*.*", "Parça listesi (*.md;*.csv;*.txt)|*.md;*.csv;*.txt|Tüm dosyalar (*.*)|*.*"); } }
        public static string ImportedFrom(string file, int n) { return L(N(n, "row", "rows") + " filled from " + file + ".", file + " dosyasından " + n + " satır dolduruldu."); }
        public static string ConflictsFound(int n) { return L("Different codes found for " + N(n, "row", "rows") + ", click.", n + " satırda farklı kodlar bulundu, tıkla."); }
        public static string ConflictsDetail { get { return L("The parts of these rows map to different codes in the file; enter them by hand:", "Bu satırların parçaları dosyada farklı kodlara çıkıyor; elle gir:"); } }
        public static string ProductionFiles { get { return L("Production files", "Üretim dosyaları"); } }
        public static string BuildConfirm(string dir, int boards)
        {
            return L("Production files will be written to " + dir + ":\n\n" +
                     "•  JLC gerber + drill package (zip)\n•  JLC BOM and CPL (assembly) files\n•  IPC-D-356 netlist\n•  Summary with charts (xlsx): cost and stock for " + N(boards, "board", "boards") + "\n\n" +
                     "Part information (manufacturer, price, stock) comes from JLC; without internet these columns stay empty.\n" +
                     "The last saved state of the files is used; if they are open in KiCad, save first.\n\nContinue?",
                     "Üretim dosyaları " + dir + " klasörüne yazılacak:\n\n" +
                     "•  JLC gerber + delik paketi (zip)\n•  JLC BOM ve CPL (montaj) dosyaları\n•  IPC-D-356 netlist\n•  Grafikli özet (xlsx): " + boards + " kart için maliyet ve stok\n\n" +
                     "Parça bilgileri (üretici, fiyat, stok) JLC'den alınır; internet yoksa bu sütunlar boş kalır.\n" +
                     "Dosyaların son kaydedilmiş hali kullanılır; KiCad'de açıksa önce kaydet.\n\nDevam edilsin mi?");
        }
        public static string Building { get { return L("Building…", "Oluşturuluyor…"); } }
        public static string BuildingFiles { get { return L("Building production files…", "Üretim dosyaları oluşturuluyor…"); } }
        public static string BuildFailed { get { return L("Production files could not be built. Click for details.", "Üretim dosyaları oluşturulamadı. Ayrıntı için tıkla."); } }
        public static string BuildDoneWithWarnings(int n)
        {
            return L("Production files are ready, with " + N(n, "warning", "warnings") + ". Click for details.", "Üretim dosyaları hazır, " + n + " uyarı var. Ayrıntı için tıkla.");
        }
        public static string BuildDone(string files) { return L("Production files are ready: " + files + "  — show them with \"Open folder\"", "Üretim dosyaları hazır: " + files + "  — \"Klasörü aç\" ile göster"); }
        public static string WarningsHeader { get { return L("Warnings:", "Uyarılar:"); } }
        public static string CreatedFilesHeader { get { return L("Created files:", "Oluşturulan dosyalar:"); } }

        public static string HelpText
        {
            get
            {
                return L("1)  On the left choose the manufacturer, layers, thickness, copper and the other options. Every rule and the stackup are shown on the right.\n\n" +
                         "2)  Press Apply. If KiCad is open, restart it.\n\n" +
                         "3)  New board:  File › New Project from Template › User Templates › DRC_…\n" +
                         "     (plain File › New Project always opens with KiCad's own rules)\n\n" +
                         "4)  Check:  PCB Editor › File › Board Setup › Board Stackup / Constraints / Custom Rules\n\n" +
                         "5)  While designing:  Inspect › Design Rules Checker › Run DRC\n\n" +
                         "6)  When ordering, choose the same layers, thickness, copper and stackup on the manufacturer's site.\n\n" +
                         "Applying to an existing project (e.g. 4 → 6 layers):  choose the target settings on the left and select the project's\n" +
                         ".kicad_pro file with \"Apply to existing project…\". The project must be closed in KiCad; a backup is taken first and drawings are not touched.\n\n" +
                         "Press Apply again after a KiCad update.  \"Restore defaults\" undoes everything.  EN / TR at the top right switches the language.",
                         "1)  Soldan üreticiyi, katmanı, kalınlığı, bakırı ve diğer seçenekleri seç. Sağda uygulanacak her kural ve stackup görünür.\n\n" +
                         "2)  Uygula'ya bas. KiCad açıksa kapatıp aç.\n\n" +
                         "3)  Yeni kart:  File › New Project from Template › User Templates › DRC_…\n" +
                         "     (düz File › New Project her zaman KiCad'in kendi kurallarıyla açılır)\n\n" +
                         "4)  Kontrol:  PCB Editor › File › Board Setup › Board Stackup / Constraints / Custom Rules\n\n" +
                         "5)  Tasarım sırasında:  Inspect › Design Rules Checker › Run DRC\n\n" +
                         "6)  Siparişte üreticinin sitesinde aynı katman, kalınlık, bakır ve stackup'ı seç.\n\n" +
                         "Mevcut bir projeye uygulamak (ör. 4 → 6 katman):  soldan hedef ayarları seç, \"Mevcut projeye uygula…\" ile\n" +
                         "projenin .kicad_pro dosyasını seç. Proje KiCad'de kapalı olmalı; önce yedek alınır, çizimlere dokunulmaz.\n\n" +
                         "KiCad güncellemesinden sonra tekrar Uygula'ya bas.  \"Varsayılana dön\" her şeyi geri alır.  Sağ üstteki EN / TR dili değiştirir.");
            }
        }
    }
}
