using System;
using System.Collections.Generic;
using System.Linq;

namespace KiCadDrc
{
    static partial class Tx
    {
        // ---------------------------------------------------------------- manufacturer options
        public static string MaskName(string code)
        {
            switch (code)
            {
                case "green": return L("Green", "Yeşil");
                case "red": return L("Red", "Kırmızı");
                case "yellow": return L("Yellow", "Sarı");
                case "blue": return L("Blue", "Mavi");
                case "purple": return L("Purple", "Mor");
                case "black": return L("Black", "Siyah");
                case "white": return L("White", "Beyaz");
            }
            return code;
        }
        public static string FinishHasl { get { return L("HASL (leaded)", "HASL (kurşunlu)"); } }
        public static string FinishHaslLeadFree { get { return L("HASL (lead-free)", "HASL (kurşunsuz)"); } }
        public static string EdgeVcut { get { return L("V-cut (edge 0.4 mm)", "V-cut (kenar 0.4 mm)"); } }
        public static string EdgeRouted { get { return L("Routed / mouse bites (edge 0.2 mm)", "Freze / mouse bites (kenar 0.2 mm)"); } }
        public static string RoutedShort { get { return L("routed", "freze"); } }
        public static string LayerCountNotMade(string maker, int layers, string options)
        {
            return L(maker + " does not make " + layers + " layer boards. Options: " + options, maker + " " + layers + " katman üretmiyor. Seçenekler: " + options);
        }
        public static string LayerCountNotMadeKept(string maker, int layers)
        {
            return L(maker + " does not make " + layers + " layer boards; the selection on the left was not changed.", maker + " " + layers + " katman üretmiyor; soldaki seçim değiştirilmedi.");
        }
        public static string StackupNotMatched(string maker)
        {
            return L("The project's stackup did not match any in the " + maker + " list; the recommended stackup was selected.",
                     "Projenin stackup'ı " + maker + " listesindekilerle eşleşmedi; önerilen stackup seçildi.");
        }
        public static string NoStackupForCombination { get { return L("There is no stackup for this combination.", "Bu kombinasyon için stackup yok."); } }

        public static string JlcHint(string field)
        {
            switch (field)
            {
                case "layers": return L("Number of copper layers on your board. Choosing it sets everything else to JLC's recommendation.",
                                        "Kartındaki bakır katman sayısı. Katmanı seçince diğer her şey JLC'nin önerdiğine ayarlanır.");
                case "thickness": return L("JLC's standard thickness is 1.6 mm. Do not change it unless you have a mechanical need (connector, enclosure).",
                                           "JLC'nin standart kalınlığı 1.6 mm. Özel bir mekanik ihtiyacın (konnektör, kasa) yoksa değiştirme.");
                case "outer": return L("Outer layer copper. 1 oz is standard. 2 oz for high current, but minimum track/clearance grows and the price rises.",
                                       "Dış katman bakırı. 1 oz standart. Yüksek akım için 2 oz; ama min iz/aralık büyür ve fiyat artar.");
                case "inner": return L("Inner layer copper. JLC's default is 0.5 oz. Do not change it unless inner layers carry high current.",
                                       "İç katman bakırı. JLC varsayılanı 0.5 oz. İç katmanlardan yüksek akım geçirmiyorsan değiştirme.");
                case "stackup": return L("Materials and thicknesses between the layers. Unless you do impedance calculations keep the ★ recommended one (JLC default); choose the same one when ordering.",
                                         "Katmanlar arasındaki malzeme ve kalınlıklar. Empedans hesabı yapmıyorsan ★ önerilen (JLC varsayılanı) kalsın; siparişte aynısını seç.");
                case "mask": return L("Solder mask colour. Green is standard; with black/white the mask bridge between pads is 0.13 mm instead of 0.10.",
                                      "Lehim maskesi rengi. Yeşil standart; siyah/beyazda pad arası maske köprüsü 0.10 yerine 0.13 mm.");
                case "finish": return L("HASL is economical. ENIG gives a flatter surface for fine pitch QFN/BGA. No HASL for 6+ layers. Lead-free if RoHS is needed.",
                                        "HASL ekonomik. İnce hatveli QFN/BGA için ENIG daha düz yüzey verir. 6+ katmanda HASL yok. RoHS gerekiyorsa kurşunsuz.");
                case "edge": return L("If JLC panelises with V-cut, copper must be 0.4 mm from the edge; routed needs 0.2. If unsure choose V-cut.",
                                      "JLC kartları V-cut ile panellerse bakır kenardan 0.4 mm uzak olmalı, frezede 0.2 yeter. Emin değilsen V-cut.");
                case "margin": return L("Safety margin added to the manufacturer's limits. 0.05 mm is recommended; more is safer but takes space on dense boards.",
                                        "Üreticinin sınırlarına eklenen güvenlik payı. 0.05 mm önerilir; artırmak daha güvenli ama yoğun kartta yer daraltır.");
            }
            return "";
        }

        // ---------------------------------------------------------------- rule table
        public static string Multilayer { get { return L("multilayer", "çok katman"); } }
        public static string TwoLayer { get { return L("2 layer", "2 katman"); } }
        public static string InnerShort { get { return L("inner", "iç"); } }
        public static string InnerLayerShort { get { return L("inner layer", "iç katman"); } }
        public static string TrackShort { get { return L("track", "iz"); } }
        public static string ClearanceShort { get { return L("clearance", "aralık"); } }
        public static string Forbidden { get { return L("forbidden", "yasak"); } }

        public static string BoardRuleName(string key)
        {
            switch (key)
            {
                case "min_track_width": return L("Min track width", "Min iz genişliği");
                case "min_clearance": return L("Min copper clearance", "Min bakır aralığı");
                case "min_connection": return L("Min connection width", "Min bağlantı genişliği");
                case "min_through_hole_diameter": return L("Min drill", "Min delik");
                case "min_via_diameter": return L("Min via diameter", "Min via çapı");
                case "min_via_annular_width": return L("Min via annular ring", "Min via halkası");
                case "min_hole_clearance": return L("Hole to copper", "Delik - bakır arası");
                case "min_hole_to_hole": return L("Hole to hole (pad)", "Delikten deliğe (pad)");
                case "min_copper_edge_clearance": return L("Copper to board edge", "Bakır - kart kenarı");
                case "solder_mask_to_copper_clearance": return L("Mask opening to track", "Maske açıklığı - iz");
                case "min_silk_clearance": return L("Silk to pad", "Silk - pad arası");
                case "min_text_height": return L("Min text height", "Min yazı yüksekliği");
                case "min_text_thickness": return L("Min text thickness", "Min yazı kalınlığı");
            }
            return key;
        }

        // Custom rule names. They are written into .kicad_dru and appear in KiCad's DRC messages; the verifier looks for the
        // same names, so a name must not be contained in another rule's name that could fire on the same test item.
        public static string Rule(string id)
        {
            switch (id)
            {
                case "pth_hole": return L("PTH pad hole", "PTH pad deliği");
                case "pth_min_warning": return L("PTH ≥0.5 recommendation (warning)", "PTH ≥0.5 önerisi (uyarı)");
                case "via_in_pad": return L("Via-in-pad via hole", "Via-in-pad via deliği");
                case "npth_hole": return L("NPTH hole", "NPTH delik");
                case "plated_slot": return L("Plated slot", "Kaplamalı slot");
                case "npth_slot": return L("Non-plated slot", "Kaplamasız slot");
                case "castellated_hole": return L("Castellated hole", "Castellated delik");
                case "castellated_spacing": return L("Castellated hole spacing", "Castellated delik aralığı");
                case "pth_ring": return L("PTH pad annular ring", "PTH pad halkası");
                case "npth_ring": return L("NPTH pad annular ring", "NPTH pad halkası");
                case "via_hole_to_hole": return L("Via hole to hole", "Via delikten deliğe");
                case "via_hole_track": return L("Via hole to track", "Via deliği - iz");
                case "min_smd_pad": return L("Min SMD pad (warning)", "Min SMD pad (uyarı)");
                case "no_blind_vias": return L("No blind/buried/micro vias", "Kör/gömülü/mikro via yok");
            }
            return id;
        }

        public static string SrcMinDrill { get { return L("JLC recommended min via hole 0.2 (0.15 costs extra)", "JLC önerilen min via deliği 0.2 (0.15 ek ücretli)"); } }
        public static string SrcMinVia { get { return L("JLC: with a 0.2 hole, diameters below 0.45 cost extra", "JLC: 0.2 delikte 0.45 altı çap ek ücretli"); } }
        public static string SrcViaRing { get { return L("JLC: via diameter 0.15 larger than the hole is recommended", "JLC: via çapı delikten 0.15 büyük önerilir"); } }
        public static string SrcHoleClearance(bool multilayer)
        {
            return L("JLC PTH to track min 0.28, recommended 0.35" + (multilayer ? " (inner layer PTH 0.3)" : ""),
                     "JLC PTH-iz min 0.28, önerilen 0.35" + (multilayer ? " (iç katman PTH 0.3)" : ""));
        }
        public static string SrcPadHoleToHole { get { return "JLC pad hole-to-hole 0.45"; } }
        public static string SrcMaskToTrack { get { return L("JLC: mask opening to track 0.09", "JLC: maske açıklığı ile iz arası 0.09"); } }
        public static string SrcSilkClearance { get { return L("JLC pad to silkscreen 0.15 (no margin)", "JLC pad to silkscreen 0.15 (pay yok)"); } }
        public static string SrcTextHeight { get { return L("JLC min text height 1.0 (no margin)", "JLC min text height 1.0 (pay yok)"); } }
        public static string SrcTextThickness { get { return L("JLC min line width 0.15 (no margin)", "JLC min line width 0.15 (pay yok)"); } }
        public static string GroupCustomRule { get { return L("Custom rule", "Özel kural"); } }
        public static string SrcDrillRange { get { return L("JLC drill 0.15-6.3 (free from 0.2)", "JLC drill 0.15-6.3 (ücretsiz min 0.2)"); } }
        public static string SrcPthWarning { get { return L("JLC: PTH ≥0.5 recommended so mask/tin does not plug it (normal on thermal via pads)", "JLC: maske/kalay tıkanmasın diye PTH ≥0.5 önerilir (termal via pad'lerinde normal)"); } }
        public static string SrcViaInPad { get { return L("JLC: via-in-pad is default from 6 layers, 0.15-0.55 vias", "JLC: 6+ katmanda via-in-pad varsayılan, 0.15-0.55 via"); } }
        public static string SrcNpth { get { return L("JLC min NPTH 0.5, NPTH to track 0.2", "JLC min NPTH 0.5, NPTH-iz 0.2"); } }
        public static string SlotLengthRule { get { return L("length ≥ 2×width", "boy ≥ 2×en"); } }
        public static string SrcPlatedSlot(string group, double slot)
        {
            return L("JLC " + group + " plated slot " + S.F(slot) + ", length at least 2×width", "JLC " + group + " kaplamalı slot " + S.F(slot) + ", boy en az 2×genişlik");
        }
        public static string SrcNpthSlot { get { return L("JLC min non-plated slot 1.0", "JLC min kaplamasız slot 1.0"); } }
        public static string SrcCastellated { get { return L("JLC castellated hole ≥0.5", "JLC castellated delik ≥0.5"); } }
        public static string SrcCastellatedSpacing { get { return L("JLC castellated hole to hole ≥0.5", "JLC castellated delikten deliğe ≥0.5"); } }
        public static string SrcPthRing(string group, bool heavy, double min, double recommended)
        {
            string values = heavy ? "2 oz: 0.254" : L("1 oz: min " + S.F(min) + ", recommended " + S.F(recommended), "1 oz: min " + S.F(min) + ", önerilen " + S.F(recommended));
            return "JLC " + group + " " + values + L("; plated pads below 0.5 behave like vias", "; 0.5 altı delikli pad'ler via gibi");
        }
        public static string SrcNpthRing { get { return L("JLC: copper NPTH pad ring ≥0.45 recommended", "JLC: bakırlı NPTH pad halkası ≥0.45 önerilir"); } }
        public static string SrcMinSmdPad { get { return L("JLC min SMD pad 0.25×0.25; 0.2-0.25 BGA pads need ENIG", "JLC min SMD pad 0.25×0.25; 0.2-0.25 BGA pad ENIG ister"); } }
        public static string SrcNoBlind { get { return L("JLC does not make blind/buried vias", "JLC blind/buried via yapmıyor"); } }
        public static string GroupPresets { get { return L("Preset sizes", "Hazır boyutlar"); } }
        public static string TrackWidths { get { return L("Track widths", "İz genişlikleri"); } }
        public static string StartsAtLimit { get { return L("Starts at the manufacturer limit", "Üretici sınırından başlar"); } }
        public static string ViaDiameterDrill { get { return L("Via (diameter/drill)", "Via (çap/delik)"); } }
        public static string StartsAtLimitAllValid { get { return L("Starts at the limit, all comply with the rules", "Sınırdan başlar, hepsi kurallara uyar"); } }
        public static string TrackClearance { get { return L("Track / clearance", "İz / aralık"); } }
        public static string SrcNetClassClearance { get { return L("Clearance ≥ JLC SMD pad-pad 0.15 + margin", "Aralık ≥ JLC SMD pad-pad 0.15 + pay"); } }
        public static string ViaDiameterDrillShort { get { return L("Via diameter / drill", "Via çap / delik"); } }
        public static string SrcNetClassVia { get { return L("Satisfies all via rules (JLC free size)", "Tüm via kurallarını sağlar (JLC ücretsiz boyut)"); } }
        public static string GroupTemplate { get { return L("Template", "Şablon"); } }
        public static string StackupSource(int layers, string total, string order)
        {
            return L(layers + " layers, " + total + " (order: " + order + ")", layers + " katman, " + total + " (sipariş: " + order + ")");
        }
        public static string MaskBridge { get { return L("Mask bridge (min web)", "Maske köprüsü (min web)"); } }
        public static string MaskExpansion { get { return L("Mask expansion", "Maske açıklığı"); } }
        public static string MaskSilkColor { get { return L("Mask / silk colour", "Maske / silk rengi"); } }
        public static string View3D { get { return L("3D view", "3D görünüm"); } }
        public static string SurfaceFinish { get { return L("Surface finish", "Yüzey kaplama"); } }
        public static string SrcNoHasl6 { get { return L("JLC: no HASL from 6 layers", "JLC: 6+ katmanda HASL yok"); } }
        public static string ViaFilledTented { get { return L("filled + capped, tented", "dolgulu + kapaklı, tented"); } }
        public static string SrcViaInPadDefault { get { return L("JLC: via-in-pad is default from 6 layers", "JLC 6+ katmanda via-in-pad varsayılan"); } }
        public static string JlcDefault { get { return L("JLC default", "JLC varsayılan"); } }
        public static string ZoneHatch { get { return L("Zone hatch line / gap", "Zone tarama çizgi / boşluk"); } }
        public static string SilkText { get { return L("Silk text", "Silk yazı"); } }
        public static string SrcSilkRatio { get { return L("JLC recommended thickness:height 1:6", "JLC önerilen kalınlık:yükseklik 1:6"); } }
        public static string NoteEngineering { get { return L("Above 10 layers JLC recommends sending the stackup for engineering review before ordering.", "JLC 10 katman üstünde siparişten önce stackup'ın mühendislik onayına gönderilmesini öneriyor."); } }
        public static string NoteHeavyCopper { get { return L("The page lists no PTH ring and mask bridge for 2.5 oz and above; the 2 oz values were used.", "Sayfada 2.5 oz ve üzeri için PTH halkası ve maske köprüsü yok; 2 oz değerleri kullanıldı."); } }
        public static string NoteThick { get { return L("JLC's page lists 2.5 mm and above for 12+ layers.", "JLC sayfası 2.5 mm ve üzerini 12+ katman için listeliyor."); } }
        public static string NoteBga { get { return L("If you use 0.2-0.25 mm BGA pads the finish must be ENIG.", "0.2-0.25 mm BGA pad kullanacaksan yüzey ENIG olmalı."); } }
        public static string CopperSummary(string outer, string inner, string margin)
        {
            return L("outer " + outer + (inner != null ? ", inner " + inner : "") + " · margin +" + margin + " mm",
                     "dış " + outer + (inner != null ? ", iç " + inner : "") + " · pay +" + margin + " mm");
        }
        public static string OrderSameOptions(string maker)
        {
            return L("When ordering, choose the same layers, thickness, copper and stackup on the " + maker + " site.",
                     "Sipariş verirken " + maker + " sitesinde aynı katman, kalınlık, bakır ve stackup'ı seç.");
        }
    }
}
