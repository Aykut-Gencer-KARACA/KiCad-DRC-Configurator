using System;
using System.Collections.Generic;
using System.Linq;

namespace KiCadDrc
{
    static partial class Tx
    {
        // ---------------------------------------------------------------- production
        public static string CatCapacitor { get { return L("Capacitor", "Kondansatör"); } }
        public static string CatResistor { get { return L("Resistor", "Direnç"); } }
        public static string CatInductor { get { return L("Inductor", "Bobin"); } }
        public static string CatFerrite { get { return L("Ferrite", "Ferrit"); } }
        public static string CatDiode { get { return L("Diode / LED", "Diyot / LED"); } }
        public static string CatTransistor { get { return L("Transistor / MOSFET", "Transistör / MOSFET"); } }
        public static string CatIc { get { return L("IC", "Entegre"); } }
        public static string CatConnector { get { return L("Connector", "Konnektör"); } }
        public static string CatFuse { get { return L("Fuse", "Sigorta"); } }
        public static string CatCrystal { get { return L("Crystal / oscillator", "Kristal / osilatör"); } }
        public static string CatSwitch { get { return L("Switch / relay", "Anahtar / röle"); } }
        public static string CatTestPoint { get { return L("Test point", "Test noktası"); } }
        public static string CatOther { get { return L("Other", "Diğer"); } }
        public static string SchematicNotFound(string path) { return L("The project's schematic was not found:\n", "Projenin şeması bulunamadı:\n") + path; }
        public static string CodesMissing(int n)
        {
            return L(N(n, "row has", "rows have") + " a missing or invalid LCSC code (e.g. C14663). Untick \"Place\" for parts that will not be assembled.",
                     n + " satırın LCSC kodu eksik ya da geçersiz (örn. C14663). Dizilmeyecek parçaların \"Dizilsin\" işaretini kaldır.");
        }
        public static string LayerMismatch(int project, int selected)
        {
            return L("The project has " + project + " layers but the selection on the left has " + selected + ". To keep the order summary correct, " +
                     "select the project's settings on the left first (or update the project with \"Apply to existing project\").",
                     "Proje " + project + " katman ama soldaki seçim " + selected + " katman. Sipariş özeti yanlış olmasın diye " +
                     "önce soldan projenin ayarlarını seç (ya da \"Mevcut projeye uygula\" ile projeyi güncelle).");
        }
        public static string PartsNotFetched(int n)
        {
            return L("Information for " + N(n, "part", "parts") + " could not be fetched from JLC (internet connection?); their price/stock columns in the xlsx are empty.",
                     n + " parçanın bilgisi JLC'den alınamadı (internet bağlantısı?); xlsx'te bu parçaların fiyat/stok sütunları boş.");
        }
        public static string NoReference { get { return L("(no reference)", "(referanssız)"); } }
        public static string PartsNotOnBoard(string refs)
        {
            return L("These parts are in the BOM but have no footprint on the PCB, so they could not be placed: " + refs +
                     "\nUpdate the PCB from the schematic (PCB Editor › Tools › Update PCB from Schematic), or untick \"Place\" for them. Nothing was written.",
                     "Bu parçalar BOM'da var ama PCB'de footprint'leri yok, dizilemezler: " + refs +
                     "\nPCB'yi şemadan güncelle (PCB Editor › Tools › Update PCB from Schematic) ya da \"Dizilsin\" işaretlerini kaldır. Hiçbir dosya yazılmadı.");
        }
        public static string CplLeftOut(string refs)
        {
            return L("Footprints on the board that are not in the placed BOM were left out of the CPL (fiducials, logos, unannotated parts): " + refs,
                     "Kartta olup dizilecek BOM'da olmayan footprint'ler CPL'e alınmadı (fiducial, logo, numaralanmamış parça): " + refs);
        }
        public static string RotationsCorrected(string list)
        {
            return L("Rotation corrected for JLC (KiCad library footprints whose zero orientation differs from JLC's); check these in JLC's preview: " + list,
                     "JLC için dönüşü düzeltilen parçalar (sıfır yönü JLC'den farklı olan KiCad kütüphane footprint'leri); JLC önizlemesinde bunlara bak: " + list);
        }
        public static string PolarisedCheck(string list)
        {
            return L("Polarised capacitors with KiCad footprints were not rotated (JLC's orientation depends on the part): in JLC's preview check that their + matches the + on the board, and choose \"Confirm Parts Placement\" when ordering: " + list,
                     "KiCad footprint'li kutuplu kondansatörler döndürülmedi (JLC'deki yönleri parçaya göre değişir): JLC önizlemesinde + uçlarının karttaki + ile aynı tarafta olduğunu kontrol et ve siparişte \"Confirm Parts Placement\"ı seç: " + list);
        }
        public static string LowStock(string code, string value, long stock, int boards, int needed)
        {
            return L(code + " (" + value + "): stock " + stock + ", JLC needs " + needed + " for " + N(boards, "board", "boards") + " (with attrition / minimum quantity).",
                     code + " (" + value + "): stok " + stock + ", JLC " + boards + " kart için " + needed + " adet istiyor (fire / en az adet dahil).");
        }
        public static string AttritionUnknown(int n)
        {
            return L(N(n, "part has", "parts have") + " no attrition data (old cache, no internet): their charged quantity is taken as the plain need; the cost may be a little low.",
                     n + " parçanın fire bilgisi yok (eski önbellek, internet yok): faturalanan adet ihtiyaç kadar alındı; maliyet biraz düşük çıkabilir.");
        }
    }
}
