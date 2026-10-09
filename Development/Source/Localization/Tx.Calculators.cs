using System;

namespace KiCadDrc
{
    static partial class Tx
    {
        // ---------------------------------------------------------------- PCB calculators (common to all calculators)
        public static string TabCalculators { get { return L("PCB calculators", "PCB hesaplayıcılar"); } }
        public static string CalcMetric { get { return L("Metric", "Metrik"); } }
        public static string CalcImperial { get { return L("Imperial", "İnç"); } }
        public static string CalcModeCalculate { get { return L("Calculate", "Hesapla"); } }
        public static string CalcModeGuide { get { return L("How it works", "Nasıl çalışır?"); } }
        public static string CalcComingSoon { get { return L("This calculator is being prepared.", "Bu hesaplayıcı hazırlanıyor."); } }
        public static string CalcPlannedDescription { get { return L("Planned: it will be added in one of the next steps.", "Planlandı: sıradaki adımlardan birinde eklenecek."); } }
        public static string CalcCustomMaterial { get { return L("Custom (type Er)", "Özel (Er'yi yaz)"); } }
        public static string CalcStackupOuter(string stackup, string material, string dk)
        {
            return L("Stackup " + stackup + ": outer dielectric " + material + " (Er " + dk + ")", "Stackup " + stackup + ": dış dielektrik " + material + " (Er " + dk + ")");
        }
        public static string CalcStackupInner(string stackup, string dk)
        {
            return L("Stackup " + stackup + ": inner dielectrics (Er " + dk + ")", "Stackup " + stackup + ": iç dielektrikler (Er " + dk + ")");
        }

        // headings of the "How it works" pages
        public static string GuideWhat { get { return L("What is it for?", "Ne işe yarar?"); } }
        public static string GuideWhen { get { return L("When do I need it?", "Ne zaman gerekir?"); } }
        public static string GuideHow { get { return L("How is it calculated? (step by step)", "Nasıl hesaplanır? (adım adım)"); } }
        public static string GuideExample { get { return L("Worked example", "Örnek hesap"); } }
        public static string GuideUse { get { return L("What do I do with the result?", "Sonucu nasıl kullanırım?"); } }
        public static string GuideNotes { get { return L("Keep in mind", "Dikkat"); } }

        // shared figure labels
        public static string FigAir { get { return L("Air (Er = 1)", "Hava (Er = 1)"); } }
        public static string FigDielectric(string er) { return L("Dielectric (Er = " + er + ")", "Dielektrik (Er = " + er + ")"); }
        public static string FigPlane { get { return L("Ground plane", "Toprak düzlemi"); } }
        public static string FigTrace { get { return L("Trace", "İz"); } }
        public static string FigTime { get { return L("time", "zaman"); } }
        public static string FigVoltage { get { return L("voltage", "gerilim"); } }

        public static string CalcTitle(string id)
        {
            switch (id)
            {
                case "bandwidth": return L("Bandwidth & max conductor length", "Bant genişliği ve maks. iletken uzunluğu");
                case "impedance": return L("Conductor impedance", "İletken empedansı");
                case "conductor": return L("Conductor properties (current / temperature)", "İletken özellikleri (akım / sıcaklık)");
                case "conversion": return L("Unit conversion", "Birim dönüştürücü");
                case "diffpair": return L("Differential pairs / crosstalk", "Diferansiyel çift / karışma");
                case "embedded_resistor": return L("Embedded resistors", "Gömülü dirençler");
                case "er_effective": return L("Er effective", "Efektif Er");
                case "fusing": return L("Fusing current", "Erime (sigorta) akımı");
                case "mechanical": return L("Mechanical information", "Mekanik bilgiler");
                case "spacing": return L("Minimum conductor spacing", "Minimum iletken aralığı");
                case "ohms_law": return L("Ohm's law", "Ohm kanunu");
                case "padstack": return L("Padstack calculator", "Padstack hesaplayıcı");
                case "pdn": return L("PDN calculator", "PDN hesaplayıcı");
                case "planar_inductor": return L("Planar inductors", "Düzlemsel bobinler");
                case "ppm_xtal": return L("PPM / crystal calculator", "PPM / kristal hesaplayıcı");
                case "thermal": return L("Thermal management", "Isıl yönetim");
                case "via": return L("Via properties", "Via özellikleri");
                case "wavelength": return L("Wavelength calculator", "Dalga boyu hesaplayıcı");
                case "reactance": return L("XL / XC reactance", "XL / XC reaktans");
            }
            return id;
        }

        public static string GuideInShort { get { return L("In short", "Kısaca"); } }
        public static string GuideTry { get { return L("Try it yourself", "Kendin dene"); } }
        public static string GuideFaq { get { return L("Frequently asked questions", "Sık sorulanlar"); } }
        public static string GuideTerms { get { return L("Terms used on this page", "Bu sayfadaki terimler"); } }
        public static string FigTargetLevel { get { return L("target 3.3 V", "hedef 3.3 V"); } }
        public static string FigAbsMax { get { return L("typical input limit 3.6 V", "tipik giriş sınırı 3.6 V"); } }
        public static string FigShortTrace(string len) { return L("short trace (" + len + ")", "kısa iz (" + len + ")"); }
        public static string FigLongTrace(string len) { return L("long trace (" + len + ")", "uzun iz (" + len + ")"); }    }
}
