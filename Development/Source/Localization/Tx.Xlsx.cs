using System;
using System.Collections.Generic;
using System.Linq;

namespace KiCadDrc
{
    static partial class Tx
    {
        // ---------------------------------------------------------------- xlsx
        public static string XPage { get { return L("Page", "Sayfa"); } }
        public static string XSheetSummary { get { return L("Summary", "Özet"); } }
        public static string XSummaryTitle { get { return L("Production Summary", "Üretim Özeti"); } }
        public static string XProjectInfo { get { return L("Project information", "Proje bilgileri"); } }
        public static string XProject { get { return L("Project", "Proje"); } }
        public static string XRevision { get { return L("Revision", "Revizyon"); } }
        public static string XDate { get { return L("Date", "Tarih"); } }
        public static string XPreparedBy { get { return L("Prepared by", "Hazırlayan"); } }
        public static string XCheckedBy { get { return L("Checked by", "Kontrol eden"); } }
        public static string XApprovedBy { get { return L("Approved by", "Onaylayan"); } }
        public static string XBoardCount { get { return L("Board quantity", "Kart adedi"); } }
        public static string XBoardCountValue(int n) { return L(n + " pcs  (cost and stock check use this quantity)", n + " adet  (maliyet ve stok kontrolü bu adede göre)"); }
        public static string XProjectFolder { get { return L("Project folder", "Proje klasörü"); } }
        public static string XOrderForm(string maker) { return L(maker + " order form", maker + " sipariş formu"); }
        public static string XLayers { get { return L("Layers", "Katman (Layers)"); } }
        public static string XThickness { get { return L("PCB Thickness", "Kalınlık (PCB Thickness)"); } }
        public static string XOuterCopper { get { return L("Outer Copper", "Dış bakır (Outer Copper)"); } }
        public static string XInnerCopper { get { return L("Inner Copper", "İç bakır (Inner Copper)"); } }
        public static string XMaskColor { get { return L("PCB Color", "Maske rengi (PCB Color)"); } }
        public static string XSurfaceFinish { get { return L("Surface Finish", "Yüzey (Surface Finish)"); } }
        public static string XViaFilled { get { return L("Epoxy Filled & Capped (default from 6 layers)", "Epoxy Filled & Capped (6+ katmanda varsayılan)"); } }
        public static string XMinTrackClearance { get { return L("Min track / clearance", "Min iz / aralık"); } }
        public static string XMinDrillVia { get { return L("Min drill / via diameter", "Min delik / via çapı"); } }
        public static string XGerberPackage { get { return L("Gerber package", "Gerber paketi"); } }
        public static string XBomCpl { get { return L("BOM / CPL (assembly)", "BOM / CPL (montaj)"); } }
        public static string XCostEstimate(int boards) { return L("JLC assembly cost estimate  ·  " + N(boards, "board", "boards"), "JLC montaj maliyeti tahmini  ·  " + boards + " kart"); }
        public static string XPartsCost(int boards) { return L("Part cost (" + N(boards, "board", "boards") + ")", "Parça maliyeti (" + boards + " kart)"); }
        public static string XPartsPerBoard { get { return L("Part cost per board", "Kart başına parça maliyeti"); } }
        public static string XAssemblyType { get { return L("Assembly type", "Montaj tipi"); } }
        public static string XStandardBothSides { get { return L("Standard PCBA, both sides (parts on the bottom side)", "Standard PCBA, iki taraf (alt yüzde parça var)"); } }
        public static string XEconomicOneSide { get { return L("Economic PCBA, one side", "Economic PCBA, tek taraf"); } }
        public static string XSetupFee(bool standard, int sides)
        {
            return standard ? L("Setup fee (" + sides + " × $" + S.F(JlcCost.StandardSetupPerSide) + ")", "Kurulum ücreti (" + sides + " × $" + S.F(JlcCost.StandardSetupPerSide) + ")")
                            : L("Setup fee", "Kurulum ücreti");
        }
        public static string XStencilFee(int sides) { return L("Stencil (" + N(sides, "side", "sides") + ")", "Şablon / stencil (" + sides + " taraf)"); }
        public static string XLoadingFee(int kinds, string fee, bool standard)
        {
            return standard ? L("Feeder loading (" + N(kinds, "part kind", "part kinds") + " × $" + fee + ")", "Parça besleme (" + kinds + " çeşit × $" + fee + ")")
                            : L("Feeder loading, Extended parts (" + N(kinds, "kind", "kinds") + " × $" + fee + ")", "Parça besleme, Extended (" + kinds + " çeşit × $" + fee + ")");
        }
        public static string XSmtJoints(int joints, string fee) { return L("SMT joints (" + joints + " × $" + fee + ")", "SMT lehim noktası (" + joints + " × $" + fee + ")"); }
        public static string XThtJoints(int joints, string fee) { return L("Hand-soldered joints, THT (" + joints + " × $" + fee + ")", "Elle lehim, THT (" + joints + " × $" + fee + ")"); }
        public static string XHandLabor { get { return L("Hand-soldering labour (per order)", "Elle lehim işçiliği (sipariş başı)"); } }
        public static string XXray(int parts, string fee) { return L("X-ray of QFN/DFN/BGA parts (" + parts + " × $" + fee + ")", "QFN/DFN/BGA parçalara X-ray (" + parts + " × $" + fee + ")"); }
        public static string XPacking { get { return L("Packing (Standard PCBA)", "Paketleme (Standard PCBA)"); } }
        public static string XEstimatedTotal { get { return L("Estimated assembly total (parts + fees, without PCB)", "Tahmini montaj toplamı (parça + ücretler, PCB hariç)"); } }
        public static string XCharged(int boards) { return L("Charged by JLC (" + N(boards, "board", "boards") + ")", "JLC'nin faturaladığı adet (" + boards + " kart)"); }
        public static string XLowStockRows { get { return L("Rows with insufficient stock", "Stoğu yetersiz satır"); } }
        public static string XUnpricedRows { get { return L("Rows without a price", "Fiyatı bulunamayan satır"); } }
        public static string XCostNote(string date, string feeSource)
        {
            return L("Parts: JLC parts library (" + date + "); quantity = need + JLC attrition (doubled for two sides), at least JLC's minimum; price tier of that quantity. " +
                     "Fees: " + feeSource + ". Not included: PCB, shipping, tax, coupons, and fees JLC adds case by case (fixtures, pre-reflow soldering, " +
                     "Confirm Parts Placement $0.45, handling fee, single-board surcharge). JLC's quote is the final price.",
                     "Parçalar: JLC parça kütüphanesi (" + date + "); adet = ihtiyaç + JLC firesi (iki tarafta iki kat), en az JLC'nin alt sınırı; fiyat o adetin kademesi. " +
                     "Ücretler: " + feeSource + ". Dahil değil: PCB, kargo, vergi, kupon ve JLC'nin duruma göre eklediği ücretler (fikstür, pre-reflow lehim, " +
                     "Confirm Parts Placement $0.45, handling fee, tek kart ek ücreti). Son fiyatı JLC'nin teklifi belirler.");
        }
        public static string XNoPartInfo { get { return L("Part information could not be fetched from JLC (internet connection?). Build the files again for price and stock.", "Parça bilgileri JLC'den alınamadı (internet bağlantısı?). Fiyat ve stok için dosyaları yeniden oluştur."); } }
        public static string XDistribution { get { return L("Part distribution", "Parça dağılımı"); } }
        public static string XCategory { get { return L("Category", "Kategori"); } }
        public static string XKinds { get { return L("Kinds (BOM rows)", "Çeşit (BOM satırı)"); } }
        public static string XQtyPerBoard { get { return L("Qty (per board)", "Adet (kart başı)"); } }
        public static string XCost(int boards) { return L("Cost (" + N(boards, "board", "boards") + ")", "Maliyet (" + boards + " kart)"); }
        public static string XTotalPlaced { get { return L("Total (placed by JLC)", "Toplam (JLC dizecek)"); } }
        public static string XNotPlacedRow { get { return L("Not placed (by hand / never)", "Dizilmeyecek (elle / hiç)"); } }
        public static string XPartType { get { return L("JLC part type", "JLC parça tipi"); } }
        public static string XType { get { return L("Type", "Tip"); } }
        public static string XAssemblyFee { get { return L("Assembly fee", "Montaj ücreti"); } }
        public static string XFeePerKind(string fee) { return L("~$" + fee + " / kind", "~$" + fee + " / çeşit"); }
        public static string XNoFeePreferred { get { return L("No extra fee (Preferred Extended)", "Ek ücret yok (Preferred Extended)"); } }
        public static string XNoFee { get { return L("No extra fee", "Ek ücret yok"); } }
        public static string XApproval { get { return L("Approval", "Onay"); } }
        public static string XRole { get { return L("Role", "Görev"); } }
        public static string XName { get { return L("Name", "Ad Soyad"); } }
        public static string XSignature { get { return L("Signature", "İmza"); } }
        public static string XUploadNote
        {
            get
            {
                return L("Upload the BOM and CPL files in JLC's assembly step; check part rotations and pin 1 in JLC's preview, especially on the bottom side.",
                         "BOM ve CPL dosyalarını JLC'nin montaj adımına yükle; parça dönüşlerini ve 1 numaralı pinleri JLC önizlemesinde kontrol et, özellikle alt yüzdekileri.");
            }
        }
        public static string XQty { get { return L("Qty", "Adet"); } }
        public static string XValue { get { return L("Value", "Değer"); } }
        public static string XDescription { get { return L("Description", "Açıklama"); } }
        public static string XPackage { get { return L("Package", "Paket"); } }
        public static string XPackageLcsc { get { return L("Package (LCSC)", "Paket (LCSC)"); } }
        public static string XManufacturer { get { return L("Manufacturer", "Üretici"); } }
        public static string XJlcType { get { return L("JLC type", "JLC tipi"); } }
        public static string XStock { get { return L("Stock", "Stok"); } }
        public static string XUnitPrice { get { return L("Unit price", "Birim fiyat"); } }
        public static string XTotalBoards(int boards) { return L("Total (" + N(boards, "board", "boards") + ")", "Toplam (" + boards + " kart)"); }
        public static string XAssembly { get { return L("Assembly", "Montaj"); } }
        public static string XBomTitle { get { return L("Bill of materials (BOM)", "Malzeme listesi (BOM)"); } }
        public static string XBomSubtitle(int boards, int kinds, int parts)
        {
            return L(N(boards, "board", "boards") + "  ·  " + N(kinds, "kind", "kinds") + ", " + N(parts, "part", "parts") + " per board", boards + " kart  ·  " + kinds + " çeşit, kart başı " + parts + " parça");
        }
        public static string XNotPlaced { get { return L("Not placed", "Dizilmeyecek"); } }
        public static string XPlacedByJlc { get { return L("Placed by JLC", "JLC dizecek"); } }
        public static string XBomLegend(bool standard, int boards)
        {
            string fees = standard
                ? L("Standard PCBA: $" + S.F(JlcCost.StandardLoading) + " feeder loading for every part kind.", "Standard PCBA: her parça çeşidi için $" + S.F(JlcCost.StandardLoading) + " besleme ücreti.")
                : L("Economic PCBA: Basic and Preferred no fee, Extended $" + S.F(JlcCost.EconomicExtendedLoading) + " per kind.", "Economic PCBA: Basic ve Preferred ücretsiz, Extended çeşit başı $" + S.F(JlcCost.EconomicExtendedLoading) + ".");
            return L(fees + "  \"Charged\" = qty × " + boards + " + JLC attrition, at least JLC's minimum; the price tier and the red stock warning use it. USD.",
                     fees + "  \"Faturalanan adet\" = adet × " + boards + " + JLC firesi, en az JLC'nin alt sınırı; fiyat kademesi ve kırmızı stok uyarısı buna göre. USD.");
        }
        public static string XCplTitle { get { return L("Placement coordinates (CPL)", "Dizgi koordinatları (CPL)"); } }
        public static string XCplSubtitle(int total, int top, int bottom)
        {
            return L(N(total, "part", "parts") + " (top " + top + ", bottom " + bottom + ")", total + " parça (üst " + top + ", alt " + bottom + ")");
        }
        public static string XDocTitle(string project) { return L(project + " production file", project + " üretim dosyası"); }
        public static string XDocSubject { get { return L("BOM, CPL and order summary", "BOM, CPL ve sipariş özeti"); } }
        public static string XChartCategory { get { return L("Parts per category (per board)", "Kategoriye göre parça adedi (kart başı)"); } }
        public static string XChartType { get { return L("JLC part type (kinds)", "JLC parça tipi (çeşit)"); } }
    }
}
