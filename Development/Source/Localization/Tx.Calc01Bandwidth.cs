using System;

namespace KiCadDrc
{
    static partial class Tx
    {
        // ---------------------------------------------------------------- calculator 1: bandwidth & max conductor length
        public static string BwSummary
        {
            get { return L("How long can a trace be before it must be treated as a transmission line?", "Bir iz ne kadar uzun olunca iletim hattı gibi ele alınmalı?"); }
        }
        // short text (used when a page cannot show the full guide)
        public static string BandwidthDescription { get { return BwGuideWhat; } }

        // ---- calculator page
        public static string BwInput { get { return L("Input", "Giriş"); } }
        public static string BwInputMethod { get { return L("Input method", "Giriş yöntemi"); } }
        public static string BwRiseTime { get { return L("Rise time", "Yükselme süresi"); } }
        public static string BwFrequency { get { return L("Frequency", "Frekans"); } }
        public static string BwDielectric { get { return L("Dielectric and trace type", "Dielektrik ve iz tipi"); } }
        public static string BwMaterial { get { return L("Material", "Malzeme"); } }
        public static string BwCircuit { get { return L("Trace type", "İz tipi"); } }
        public static string BwSettings { get { return L("Method settings", "Yöntem ayarları"); } }
        public static string BwSrFactor { get { return L("Fraction of rise distance", "Yükselme mesafesi oranı"); } }
        public static string BwLambdaDivisor { get { return L("Wavelength fraction", "Dalga boyu oranı"); } }
        public static string BwTraceLength { get { return L("Your trace length (optional)", "İz uzunluğun (isteğe bağlı)"); } }
        public static string BwTraceNote { get { return L("Enter a trace length to check it against both limits.", "İki sınırla karşılaştırmak için bir iz uzunluğu gir."); } }
        public static string BwPicture { get { return L("Picture: your trace and the limits (to scale)", "Görsel: iz ve sınırlar (ölçekli)"); } }
        public static string BwIpcMethod { get { return L("IPC-2251 method (rise time)", "IPC-2251 yöntemi (yükselme süresi)"); } }
        public static string BwBandwidth { get { return L("Bandwidth (0.35 / tr)", "Bant genişliği (0.35 / tr)"); } }
        public static string BwEquivalentRise { get { return L("Equivalent rise time (0.35 / f)", "Eşdeğer yükselme süresi (0.35 / f)"); } }
        public static string BwErEff { get { return "Er_eff"; } }
        public static string BwSpeed { get { return L("Propagation speed", "Yayılma hızı"); } }
        public static string BwDelay { get { return L("Propagation delay", "Yayılma gecikmesi"); } }
        public static string BwRiseDistance { get { return L("Rise distance Sr", "Yükselme mesafesi Sr"); } }
        public static string BwMaxLength { get { return L("Maximum conductor length", "Maksimum iletken uzunluğu"); } }
        public static string BwIpcNote(string factor) { return L("Max length = " + factor + " × Sr", "Maks. uzunluk = " + factor + " × Sr"); }
        public static string BwIpcNoteFromFrequency
        {
            get { return L("Frequency input: the rise time is taken as 0.35 / f.", "Frekans girişi: yükselme süresi 0.35 / f olarak alındı."); }
        }
        public static string BwFrequencyMethod { get { return L("Frequency domain method", "Frekans yöntemi"); } }
        public static string BwWavelengthAir { get { return L("Wavelength in air", "Havadaki dalga boyu"); } }
        public static string BwWavelengthDielectric { get { return L("Wavelength on the trace", "İz üzerindeki dalga boyu"); } }
        public static string BwFrequencyNote
        {
            get { return L("Max length = wavelength in air / n (conventional; the wavelength on the trace is shorter).", "Maks. uzunluk = havadaki dalga boyu / n (alışılmış yöntem; iz üzerindeki dalga boyu daha kısadır)."); }
        }
        public static string BwVerdictNone(string limit)
        {
            return L("Traces up to " + limit + " can be routed as plain connections (the shorter of the two limits).",
                     limit + " uzunluğa kadar izler düz bağlantı olarak çizilebilir (iki sınırdan kısa olanı).");
        }
        public static string BwVerdictShort(string trace, string limit)
        {
            return L("Your " + trace + " trace is shorter than " + limit + ": no impedance control needed for this edge.",
                     trace + " iz, " + limit + " sınırından kısa: bu kenar için empedans kontrolü gerekmez.");
        }
        public static string BwVerdictLong(string trace, string limit)
        {
            return L("Your " + trace + " trace is longer than " + limit + ": treat it as a transmission line (controlled impedance, termination).",
                     trace + " iz, " + limit + " sınırından uzun: iletim hattı olarak ele al (kontrollü empedans, sonlandırma).");
        }

        // ---- figures
        public static string FigMicrostripText
        {
            get { return L("Part of the field runs in the air above the board, so the signal is faster. Er_eff lies between 1 and Er.",
                           "Alanın bir kısmı kartın üstündeki havadan geçer; sinyal daha hızlıdır. Er_eff, 1 ile Er arasındadır."); }
        }
        public static string FigStriplineText
        {
            get { return L("The whole field stays inside the dielectric between the two planes, so the signal is slower. Er_eff = Er.",
                           "Alanın tamamı iki düzlem arasındaki dielektrikte kalır; sinyal daha yavaştır. Er_eff = Er."); }
        }
        public static string FigSafeZone { get { return L("plain connection is fine", "düz bağlantı yeterli"); } }
        public static string FigCarefulZone { get { return L("transmission line: control the impedance", "iletim hattı: empedansı kontrol et"); } }
        public static string FigYourTrace(string length) { return L("your trace: " + length, "senin izin: " + length); }
        public static string FigEdge(string sr) { return L("signal edge on the trace, Sr = " + sr, "iz üzerindeki sinyal kenarı, Sr = " + sr); }
        public static string FigEdgeText
        {
            get { return L("tr: time for the edge to go from 10 % to 90 %. The steeper the edge, the higher the frequencies it contains: f ≈ 0.35 / tr.",
                           "tr: kenarın %10'dan %90'a çıkma süresi. Kenar ne kadar dikse o kadar yüksek frekans içerir: f ≈ 0.35 / tr."); }
        }
        public static string FigLambdaText
        {
            get { return L("shorter than λ/7: the voltage is nearly the same along the whole trace", "λ/7'den kısa: izin her yerinde gerilim neredeyse aynı"); }
        }

        // ---- "How it works" page
        public static string BwGuideInShort
        {
            get
            {
                return L("A signal edge is not everywhere on the trace at once: it moves along the trace and takes up a certain length (the rise distance). " +
                         "If the trace is shorter than about a quarter of that length it behaves like a wire. If it is longer, the signal bounces between the ends " +
                         "and the trace must be designed as a transmission line.",
                         "Sinyalin kenarı iz üzerinde bir anda her yerde olmaz: iz boyunca ilerler ve belli bir uzunluk kaplar (yükselme mesafesi). " +
                         "İz bu uzunluğun yaklaşık dörtte birinden kısaysa tel gibi davranır. Uzunsa sinyal iki uç arasında gidip gelir ve iz bir iletim hattı " +
                         "olarak tasarlanmalıdır.");
            }
        }
        public static string BwGuideWhat
        {
            get
            {
                return L("When a driver switches its output, the new voltage does not appear on the whole trace at once. It travels along the trace as an " +
                         "electromagnetic wave, at about half the speed of light on FR-4 (roughly 15–18 cm per nanosecond). This calculator tells you up to which " +
                         "length you can ignore this travel time, and from which length you have to design the trace for it.",
                         "Bir sürücü çıkışını değiştirdiğinde yeni gerilim izin her yerinde aynı anda oluşmaz. İz boyunca bir elektromanyetik dalga olarak ilerler; " +
                         "FR-4 üzerinde ışık hızının yaklaşık yarısıyla (nanosaniyede kabaca 15–18 cm). Bu hesaplayıcı, bu yol alma süresini hangi uzunluğa kadar " +
                         "görmezden gelebileceğini ve hangi uzunluktan sonra izi buna göre tasarlaman gerektiğini söyler.");
            }
        }
        public static string BwGuideAnalogy
        {
            get
            {
                return L("Think of flicking the end of a rope. With a short rope the whole rope moves together. With a long rope a wave runs to the far end, " +
                         "hits it and comes back; the end jerks up and down before it settles. A PCB trace does the same with voltage: on a long trace the " +
                         "receiver first sees too much voltage, then too little, then too much again (ringing). A fast edge is a quick flick – the faster the flick, " +
                         "the shorter the rope must be to still move as one piece.",
                         "Bir ipin ucunu hızlıca silkelediğini düşün. İp kısaysa tamamı birlikte hareket eder. İp uzunsa bir dalga öbür uca koşar, oraya çarpar ve geri " +
                         "döner; uç yerine oturmadan önce aşağı yukarı sallanır. Bir PCB izi gerilimle aynısını yapar: uzun izde alıcı önce fazla, sonra az, sonra " +
                         "yine fazla gerilim görür (çınlama). Hızlı bir kenar hızlı bir silkeleme gibidir: silkeleme ne kadar hızlıysa, ipin tek parça gibi " +
                         "hareket etmesi için o kadar kısa olması gerekir.");
            }
        }
        public static string BwGuideProblemTitle { get { return L("What does the problem look like?", "Sorun nasıl görünür?"); } }
        public static string BwGuideProblem
        {
            get
            {
                return L("The picture shows what the receiver sees when a 3.3 V driver switches with a 1 ns edge, once over a short and once over a long trace. " +
                         "On the short trace the voltage rises cleanly to 3.3 V. On the long trace the wave reflects at the receiver and at the driver: the voltage " +
                         "shoots far above 3.3 V (beyond what the input may take), falls back, and rings for several nanoseconds. A clock input can count such an " +
                         "edge twice, and the overshoot stresses the chip and radiates noise.",
                         "Şekil, 3.3 V'luk bir sürücü 1 ns'lik bir kenarla anahtarlandığında alıcının gördüğünü gösteriyor; bir kısa, bir de uzun izde. " +
                         "Kısa izde gerilim temiz şekilde 3.3 V'a çıkıyor. Uzun izde dalga alıcıda ve sürücüde yansıyor: gerilim 3.3 V'un çok üstüne " +
                         "(girişin dayanabileceğinden fazlasına) fırlıyor, geri düşüyor ve birkaç nanosaniye çınlıyor. Bir saat girişi böyle bir kenarı iki kez " +
                         "sayabilir; aşma da entegreyi zorlar ve gürültü yayar.");
            }
        }
        public static string BwGuideFigProblem
        {
            get { return L("Figure 1 – Receiver voltage for a 1 ns edge on FR-4 microstrip: 20 mm trace (green) and 200 mm trace (red).",
                           "Şekil 1 – FR-4 microstrip üzerinde 1 ns'lik kenar için alıcı gerilimi: 20 mm iz (yeşil) ve 200 mm iz (kırmızı)."); }
        }
        public static string BwGuideTry
        {
            get
            {
                return L("Drag the slider to change the trace length (1 ns edge, FR-4, microstrip). The curve is recalculated at once. Watch what happens " +
                         "around the critical length of 44 mm that this calculator gives for these values.",
                         "Kaydırıcıyla iz uzunluğunu değiştir (1 ns kenar, FR-4, microstrip). Eğri anında yeniden hesaplanır. Bu hesaplayıcının bu değerler için " +
                         "verdiği 44 mm'lik kritik uzunluğun çevresinde ne olduğuna bak.");
            }
        }
        public static string BwTryInfo(string length, string roundTrip, string critical)
        {
            return L("Trace length: " + length + " mm   ·   round trip: " + roundTrip + " ns   ·   critical length: " + critical + " mm",
                     "İz uzunluğu: " + length + " mm   ·   gidiş-dönüş: " + roundTrip + " ns   ·   kritik uzunluk: " + critical + " mm");
        }
        public static string BwTryClean(string over) { return L("Clean edge (overshoot " + over + " %)", "Temiz kenar (aşma %" + over + ")"); }
        public static string BwTryRinging(string over) { return L("Ringing: overshoot " + over + " %", "Çınlama: aşma %" + over); }
        public static string BwTryWhyClean
        {
            get { return L("The wave's round trip is short compared with the edge, so the reflections overlap with the rising edge and smooth out.",
                           "Dalganın gidiş-dönüşü kenara göre kısa; yansımalar yükselen kenarın içinde kalıp birbirini yumuşatıyor."); }
        }
        public static string BwTryWhyRinging
        {
            get { return L("The round trip is longer than the edge: each reflection arrives as a separate step above or below 3.3 V. " +
                           "A termination (series resistor at the driver) or a shorter trace would remove it.",
                           "Gidiş-dönüş kenardan uzun: her yansıma 3.3 V'un üstünde ya da altında ayrı bir basamak olarak geliyor. " +
                           "Bir sonlandırma (sürücüye seri direnç) ya da daha kısa bir iz bunu giderir."); }
        }
        public static string[] BwGuideWhen
        {
            get
            {
                return Turkish
                    ? new[] { "Hızlı kenarlı sinyallerde: saatler, SPI, SDIO, Ethernet, USB, HDMI, DDR.",
                              "Frekansı düşük ama kenarı hızlı sinyallerde de: modern mikrodenetleyici çıkışlarının kenarı 1–5 ns'dir; 1 kHz'lik bir PWM bile bu kenarlarla anahtarlanır.",
                              "Bir ize kontrollü empedans gerekip gerekmediğine karar verirken.",
                              "Yavaş sinyaller (LED, röle, 100 ns'den yavaş kenarlar) için genelde gerekmez." }
                    : new[] { "Fast-edged signals: clocks, SPI, SDIO, Ethernet, USB, HDMI, DDR.",
                              "Low-frequency signals with fast edges too: modern microcontroller outputs switch in 1–5 ns; even a 1 kHz PWM uses such edges.",
                              "When you decide whether a trace needs controlled impedance.",
                              "Slow signals (LEDs, relays, edges slower than 100 ns) usually do not need it." };
            }
        }
        public static string BwGuideStep1Title { get { return L("Find the edge of the signal (rise time)", "Sinyalin kenarını bul (yükselme süresi)"); } }
        public static string BwGuideStep1
        {
            get
            {
                return L("What matters is how fast the signal switches, not how often. The rise time tr is the time the edge needs from 10 % to 90 % of " +
                         "the swing; take it from the driver's datasheet (often called rise/fall time or slew rate). A steep edge is made of many sine waves; " +
                         "the highest one that still matters is about 0.35 / tr (the bandwidth of the edge).",
                         "Önemli olan sinyalin ne sıklıkla değil, ne hızlı değiştiğidir. Yükselme süresi tr, kenarın salınımın %10'undan %90'ına çıkma süresidir; " +
                         "sürücünün datasheet'inden al (genelde rise/fall time ya da slew rate diye geçer). Dik bir kenar birçok sinüs dalgasından oluşur; " +
                         "hâlâ önemli olan en yükseği yaklaşık 0.35 / tr'dir (kenarın bant genişliği).");
            }
        }
        public static string BwGuideFig1 { get { return L("Figure 2 – A signal edge and its rise time tr.", "Şekil 2 – Bir sinyal kenarı ve yükselme süresi tr."); } }
        public static string BwGuideFormula1Note { get { return L("   bandwidth of the edge (a 1 ns edge contains frequencies up to about 350 MHz)", "   kenarın bant genişliği (1 ns'lik bir kenar yaklaşık 350 MHz'e kadar frekans içerir)"); } }
        public static string BwGuideStep2Title { get { return L("How fast does the signal travel on the board?", "Sinyal kartta ne hızla ilerler?"); } }
        public static string BwGuideStep2
        {
            get
            {
                return L("In vacuum a signal travels at the speed of light c (30 cm per ns). In a dielectric it is slower by √Er. On an outer layer (microstrip) " +
                         "part of the field runs in the air above the board, so the signal sees an effective Er_eff between 1 and Er and is faster than on an " +
                         "inner layer (stripline), where the whole field stays in the dielectric.",
                         "Boşlukta sinyal ışık hızıyla (c, nanosaniyede 30 cm) gider. Dielektrik içinde √Er kadar yavaşlar. Dış katmanda (microstrip) alanın bir " +
                         "kısmı kartın üstündeki havadan geçer; bu yüzden sinyal 1 ile Er arasında bir efektif Er_eff görür ve alanın tamamının dielektrikte " +
                         "kaldığı iç katmana (stripline) göre daha hızlıdır.");
            }
        }
        public static string BwGuideFig2a { get { return L("Figure 3a – Microstrip: trace on an outer layer, part of the field in the air.", "Şekil 3a – Microstrip: dış katmandaki iz, alanın bir kısmı havada."); } }
        public static string BwGuideFig2b { get { return L("Figure 3b – Stripline: trace between two planes, the whole field in the dielectric.", "Şekil 3b – Stripline: iki düzlem arasındaki iz, alanın tamamı dielektrikte."); } }
        public static string BwGuideFormula2Note { get { return L("   c = 299 792 458 m/s (speed of light)", "   c = 299 792 458 m/s (ışık hızı)"); } }
        public static string BwGuideStep3Title { get { return L("How long is the edge on the trace? (rise distance)", "Kenar iz üzerinde ne kadar yer kaplar? (yükselme mesafesi)"); } }
        public static string BwGuideStep3
        {
            get
            {
                return L("During the rise time the edge travels the rise distance Sr = tr · v: this is the length the rising part of the signal takes up on the " +
                         "trace (the purple ramp in the picture on the Calculate page). If the trace is much shorter than Sr, the reflections come back while the " +
                         "edge is still rising and melt into it – exactly what the slider shows. The usual rule (IPC-2251): a trace shorter than a quarter of Sr " +
                         "is a plain connection.",
                         "Yükselme süresi boyunca kenar Sr = tr · v kadar yol alır: bu, sinyalin yükselen kısmının iz üzerinde kapladığı uzunluktur (Hesapla " +
                         "sayfasındaki görseldeki mor rampa). İz, Sr'den çok kısaysa yansımalar kenar daha yükselirken geri döner ve onun içinde erir; " +
                         "kaydırıcının gösterdiği tam olarak budur. Yaygın kural (IPC-2251): Sr'nin dörtte birinden kısa iz, düz bir bağlantıdır.");
            }
        }
        public static string BwGuideFormula3Note { get { return L("   k: 0.2 (strict) … 0.5 (loose)", "   k: 0.2 (sıkı) … 0.5 (gevşek)"); } }
        public static string BwGuideStep4Title { get { return L("Second check: the wavelength", "İkinci kontrol: dalga boyu"); } }
        public static string BwGuideStep4
        {
            get
            {
                return L("The same question from the frequency side: a sine wave at the bandwidth f has the wavelength λ = c / f. A trace shorter than a small " +
                         "part of it (λ/7 is common, λ/4 … λ/20 by how strict you are) carries nearly the same voltage everywhere, so it behaves like a wire. " +
                         "The calculator gives both limits; the shorter one is the one to follow.",
                         "Aynı soruya frekans tarafından bakış: bant genişliğindeki (f) bir sinüsün dalga boyu λ = c / f'dir. Bunun küçük bir parçasından " +
                         "(yaygın olarak λ/7; ne kadar sıkı olmak istediğine göre λ/4 … λ/20) kısa bir izin her yerinde gerilim neredeyse aynıdır; iz tel gibi " +
                         "davranır. Hesaplayıcı iki sınırı da verir; uyulacak olan kısa olanıdır.");
            }
        }
        public static string BwGuideFig4 { get { return L("Figure 4 – One wavelength and its λ/7 part.", "Şekil 4 – Bir dalga boyu ve λ/7'lik parçası."); } }
        public static string BwGuideExample
        {
            get
            {
                return L("Rise time tr = 1 ns, FR-4 (Er = 4.6), microstrip:\n" +
                         "•  bandwidth f = 0.35 / 1 ns = 350 MHz\n" +
                         "•  Er_eff = 0.475 · 4.6 + 0.67 = 2.86   →   v = c / √2.86 = 177 mm/ns (59 % of c)\n" +
                         "•  Sr = 1 ns · 177 mm/ns = 177 mm   →   L_max = 0.25 · 177 = 44 mm\n" +
                         "•  λ = c / 350 MHz = 857 mm   →   λ/7 = 122 mm\n" +
                         "The shorter limit is 44 mm: a 30 mm trace can be drawn as a plain connection, an 80 mm trace needs controlled impedance.",
                         "Yükselme süresi tr = 1 ns, FR-4 (Er = 4.6), microstrip:\n" +
                         "•  bant genişliği f = 0.35 / 1 ns = 350 MHz\n" +
                         "•  Er_eff = 0.475 · 4.6 + 0.67 = 2.86   →   v = c / √2.86 = 177 mm/ns (c'nin %59'u)\n" +
                         "•  Sr = 1 ns · 177 mm/ns = 177 mm   →   L_max = 0.25 · 177 = 44 mm\n" +
                         "•  λ = c / 350 MHz = 857 mm   →   λ/7 = 122 mm\n" +
                         "Kısa olan sınır 44 mm: 30 mm'lik bir iz düz bağlantı olarak çizilebilir, 80 mm'lik bir ize kontrollü empedans gerekir.");
            }
        }
        public static string[] BwGuideUse
        {
            get
            {
                return Turkish
                    ? new[] { "İzin bu uzunluktan kısaysa: normal çiz, empedans hesabına gerek yok.",
                              "Uzunsa: iz genişliğini hedef empedansa göre seç (İletken empedansı hesaplayıcısı), izin altında kesintisiz bir referans düzlemi olsun, " +
                              "gerekirse sürücünün hemen yanına seri bir direnç koy (tipik 22–33 Ω; sürücünün iç direnciyle birlikte iz empedansına yaklaşmalı).",
                              "Kenarı yavaşlatmak (sürücünün hız/slew-rate ayarı, seri direnç) kritik uzunluğu uzatır: gereğinden hızlı kenar kullanma.",
                              "Aynı ağda birden fazla alıcı varsa, en uzak alıcıya olan uzunluğu kontrol et." }
                    : new[] { "If your trace is shorter than this length: route it normally, no impedance calculation needed.",
                              "If it is longer: choose the width for the target impedance (Conductor impedance calculator), keep a solid reference plane under it " +
                              "and, if needed, add a series resistor right at the driver (typically 22–33 Ω; together with the driver's own resistance it should approach the trace impedance).",
                              "Slowing the edge (driver speed/slew-rate setting, series resistor) makes the critical length longer: do not use faster edges than needed.",
                              "With several receivers on one net, check the length to the farthest one." };
            }
        }
        public static string[][] BwGuideFaq
        {
            get
            {
                return Turkish
                    ? new[]
                    {
                        new[] { "Saat frekansım sadece 1 MHz, bu beni ilgilendirir mi?",
                                "Evet, çünkü önemli olan frekans değil kenar hızı. 1 MHz'lik bir saat de 1–2 ns'lik kenarlarla anahtarlanıyorsa 175–350 MHz'lik bileşenler içerir " +
                                "ve kritik uzunluk birkaç santimetredir. Kenarı yavaş bir sinyal (ör. 50 ns) ise metrelerce iz kaldırır." },
                        new[] { "0.25 Sr mi seçmeliyim, 0.5 Sr mı?",
                                "0.25 Sr güvenli ve yaygın olanıdır. 0.5 Sr'de yansımalar ölçülebilir ama birçok dijital devre bunu tolere eder. Saat ve hızlı veri hatlarında " +
                                "0.25 Sr kullan; yavaş kontrol sinyallerinde 0.5 Sr yeterlidir." },
                        new[] { "İç katmanda (stripline) sınır neden daha kısa?",
                                "İç katmanda alanın tamamı dielektrikte kaldığı için sinyal daha yavaş gider; aynı yükselme süresinde kenar iz üzerinde daha kısa bir yer " +
                                "kaplar, bu yüzden izin kısa sayılacağı uzunluk da kısalır." },
                        new[] { "Seri direnç tam olarak ne yapıyor?",
                                "Yansıyan dalga sürücüye döndüğünde, sürücünün iç direnci ile seri direncin toplamı iz empedansına eşitse dalga orada emilir ve bir daha geri " +
                                "yansımaz. Şekillerdeki sürücünün iç direnci 15 Ω, iz 50 Ω; yaklaşık 33 Ω'luk bir seri direnç çınlamayı neredeyse tamamen giderir." },
                        new[] { "Bu hesaplayıcı iz genişliğini söylüyor mu?",
                                "Hayır. Sadece izin empedans kontrolüne ihtiyacı olup olmadığını söyler. Gerekiyorsa genişliği İletken empedansı hesaplayıcısında bulursun." },
                    }
                    : new[]
                    {
                        new[] { "My clock is only 1 MHz – does this concern me?",
                                "Yes, because what matters is the edge speed, not the frequency. A 1 MHz clock switching with 1–2 ns edges contains 175–350 MHz components " +
                                "and its critical length is a few centimetres. A slow-edged signal (e.g. 50 ns) tolerates metres of trace." },
                        new[] { "Should I choose 0.25 Sr or 0.5 Sr?",
                                "0.25 Sr is the safe and common choice. At 0.5 Sr the reflections are measurable, but many digital circuits tolerate them. Use 0.25 Sr for " +
                                "clocks and fast data lines; 0.5 Sr is enough for slow control signals." },
                        new[] { "Why is the limit shorter on an inner layer (stripline)?",
                                "On an inner layer the whole field stays in the dielectric, so the signal is slower; with the same rise time the edge takes up less length on " +
                                "the trace, so the length at which a trace still counts as short is shorter too." },
                        new[] { "What exactly does the series resistor do?",
                                "When the reflected wave comes back to the driver, it is absorbed there if the driver's own resistance plus the series resistor equals the " +
                                "trace impedance, and it does not reflect again. In the figures the driver has 15 Ω and the trace 50 Ω; a series resistor of about 33 Ω " +
                                "removes the ringing almost completely." },
                        new[] { "Does this calculator give me the trace width?",
                                "No. It only tells whether the trace needs impedance control. If it does, find the width with the Conductor impedance calculator." },
                    };
            }
        }
        public static string[][] BwGuideTerms
        {
            get
            {
                return Turkish
                    ? new[]
                    {
                        new[] { "Yükselme süresi (tr)", "Kenarın salınımın %10'undan %90'ına çıkma süresi." },
                        new[] { "Bant genişliği", "Bir kenarın içerdiği önemli en yüksek frekans; ≈ 0.35 / tr." },
                        new[] { "Er (Dk)", "Dielektrik sabiti: malzemenin sinyali ne kadar yavaşlattığı. FR-4 ≈ 4.2–4.6." },
                        new[] { "Er_eff", "Alanın bir kısmı havadaysa (microstrip) sinyalin gördüğü ortalama Er." },
                        new[] { "Yükselme mesafesi (Sr)", "Kenarın yükselme süresi boyunca iz üzerinde aldığı yol; kenarın iz üzerindeki uzunluğu." },
                        new[] { "Dalga boyu (λ)", "Bir sinüsün bir periyotta aldığı yol: λ = c / f." },
                        new[] { "Microstrip", "Dış katmandaki iz; altında bir düzlem, üstünde hava." },
                        new[] { "Stripline", "İç katmandaki iz; iki düzlem arasında, tamamen dielektrik içinde." },
                        new[] { "İletim hattı", "Üzerindeki sinyalin yol alma süresinin önemli olduğu kadar uzun iz; empedansı kontrol edilmelidir." },
                        new[] { "Empedans (Z0)", "Bir iletim hattının dalgaya gösterdiği direnç; iz genişliği ve dielektriğe bağlıdır (tipik 50 Ω)." },
                        new[] { "Sonlandırma", "Yansımaları emen direnç (ör. sürücüye seri direnç)." },
                        new[] { "Çınlama / aşma", "Yansımalar yüzünden gerilimin hedefin üstüne çıkıp altına inerek salınması." },
                    }
                    : new[]
                    {
                        new[] { "Rise time (tr)", "Time the edge needs from 10 % to 90 % of the swing." },
                        new[] { "Bandwidth", "Highest important frequency in an edge; ≈ 0.35 / tr." },
                        new[] { "Er (Dk)", "Dielectric constant: how much the material slows the signal. FR-4 ≈ 4.2–4.6." },
                        new[] { "Er_eff", "Average Er the signal sees when part of the field is in air (microstrip)." },
                        new[] { "Rise distance (Sr)", "Distance the edge travels during its rise time; the length of the edge on the trace." },
                        new[] { "Wavelength (λ)", "Distance a sine wave travels in one period: λ = c / f." },
                        new[] { "Microstrip", "Trace on an outer layer; a plane below, air above." },
                        new[] { "Stripline", "Trace on an inner layer; between two planes, fully inside the dielectric." },
                        new[] { "Transmission line", "A trace long enough that the travel time of its signal matters; its impedance must be controlled." },
                        new[] { "Impedance (Z0)", "The resistance a transmission line presents to a wave; set by trace width and dielectric (typically 50 Ω)." },
                        new[] { "Termination", "A resistor that absorbs reflections (e.g. a series resistor at the driver)." },
                        new[] { "Ringing / overshoot", "The voltage swinging above and below its target because of reflections." },
                    };
            }
        }
        public static string BwGuideNotes
        {
            get
            {
                return L("•  The microstrip formula is an approximation that ignores the trace width; the real Er_eff varies by a few percent with the width. For stripline it is exact.\n" +
                         "•  Er depends on frequency: FR-4's 4.6 is a low-frequency value, at GHz it is about 4.2–4.4. The stackup entries use JLC's values.\n" +
                         "•  The limits are rules of thumb, not a sharp border: close to the limit, choose the safe side.\n" +
                         "•  The waveforms in the figures come from a simplified model (lossless trace, 15 Ω driver, 50 Ω trace, open receiver input); on a real " +
                         "board losses damp the ringing somewhat.",
                         "•  Microstrip formülü iz genişliğini hesaba katmayan bir yaklaşımdır; gerçek Er_eff genişliğe göre birkaç yüzde değişir. Stripline için tam doğrudur.\n" +
                         "•  Er frekansa göre değişir: FR-4'ün 4.6'sı düşük frekans değeridir, GHz'te yaklaşık 4.2–4.4'tür. Stackup seçenekleri JLC'nin değerlerini kullanır.\n" +
                         "•  Sınırlar keskin bir çizgi değil, pratik kurallardır: sınıra yakınsan güvenli tarafı seç.\n" +
                         "•  Şekillerdeki dalga biçimleri basitleştirilmiş bir modelden gelir (kayıpsız iz, 15 Ω sürücü, 50 Ω iz, açık alıcı girişi); gerçek kartta " +
                         "kayıplar çınlamayı bir miktar azaltır.");
            }
        }
    }
}
