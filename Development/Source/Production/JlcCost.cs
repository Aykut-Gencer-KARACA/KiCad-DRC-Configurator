using System;
using System.Collections.Generic;
using System.Linq;

namespace KiCadDrc
{
    // Cost of a JLC assembly order, as close to JLC's own quote as the public data allows.
    //
    // Parts (checked 2026-10-08 against JLC's live calculators jlcpcb.com/api/overseas-core-platform/shoppingCart/smtGood/
    // calculateAttrition and .../calculateComponentOrderQty: 800/800 rows equal, 1-100 boards, one and both sides):
    //   need      = parts per board × boards
    //   attrition = k × (lossNumber + floor(0.002 × max(0, need − reel size)))      k = 2 when both sides are assembled
    //   charged   = max(need + attrition, leastPatchNumber)
    //   the price tier of the part page is chosen by the charged quantity (the part pages show the same ladder as the app).
    // Assembly fees: jlcpcb.com/help/article/pcb-assembly-price, "Last updated on Sep 09, 2026". Assembly on both sides needs
    // Standard PCBA. Included: setup, stencil, feeder loading, SMT and manual joints, hand-soldering labour, X-ray of leadless
    // parts, Standard packing. Not included (the page gives no rule for when they apply, or they are options): fixtures,
    // pre-reflow soldering, Confirm Parts Placement, handling fee, single board surcharge, PCB, shipping, tax, coupons.
    class CostLine
    {
        public BomRow Row; public PartInfo Info;
        public int Need, Charged;
        public bool QuantityRuleKnown;   // false: the part's attrition fields were not available (charged = need)
        public double Unit = double.NaN, Total;
        public bool LowStock;
    }

    class AssemblyEstimate
    {
        public int Boards, Sides;
        public bool Standard;
        public List<CostLine> Lines = new List<CostLine>();
        public double PartsTotal;
        public int Unpriced, UnknownRule;
        public int SmtJointsPerBoard, ThtJointsPerBoard, LeadlessPerBoard;
        public int LoadingKinds; public double LoadingFeePerKind;
        public double SmtRate, ThtRate, XrayRate;
        public double Setup, Stencil, Loading, SmtCost, ThtCost, HandLabor, Xray, Packing;
        public double AssemblyTotal { get { return Setup + Stencil + Loading + SmtCost + ThtCost + HandLabor + Xray + Packing; } }
        public double Total { get { return PartsTotal + AssemblyTotal; } }
        public CostLine Line(BomRow r) { return Lines.FirstOrDefault(x => x.Row == r); }
    }

    static class JlcCost
    {
        public const string FeeSource = "jlcpcb.com/help/article/pcb-assembly-price (2026-09-09)";
        public const double WastageCoefficient = 0.002;
        // Economic PCBA (one side only)
        public const double EconomicSetup = 8.18, EconomicStencil = 1.53, EconomicExtendedLoading = 3.07;
        // Standard PCBA (per side for setup and stencil); feeder loading for every Basic/Extended part kind
        public const double StandardSetupPerSide = 25.56, StandardStencilPerSide = 8.21, StandardLoading = 1.53;
        // per joint / per inspected part; {from quantity, rate}, the rate of the tier the whole quantity falls in
        public static readonly double[][] EconomicSmtJoint = { new[] { 1, 0.0016 } };
        public static readonly double[][] StandardSmtJoint = { new[] { 1, 0.0016 }, new[] { 50001, 0.0013 }, new[] { 100001, 0.0012 } };
        public static readonly double[][] ManualJoint = { new[] { 1, 0.0164 }, new[] { 10001, 0.015 }, new[] { 30001, 0.012 } };
        // X-ray, "required for components such as BGA, QFN, and other leadless packages"; quantity = such parts on all boards
        public static readonly double[][] XrayPart = { new[] { 1, 1.64 }, new[] { 11, 0.82 }, new[] { 51, 0.49 }, new[] { 201, 0.33 },
            new[] { 501, 0.25 }, new[] { 1001, 0.16 }, new[] { 3001, 0.082 } };
        public const double HandSolderingLabor = 3.58;   // per order, when anything is soldered by hand (through-hole parts)
        public const double StandardPacking = 0.50;      // Standard PCBA: $0.50 + $0.00003 × PCB area (cm²); the area term is left out

        public static double Rate(double[][] tiers, long qty)
        {
            double r = tiers[0][1];
            foreach (var t in tiers) if (qty >= t[0]) r = t[1];
            return r;
        }

        // Leadless package (JLC X-rays them): QFN/DFN/SON/BGA/LGA/LCC/PowerPAK families, by footprint name
        public static bool IsLeadless(string footprint)
        {
            string name = footprint ?? ""; int colon = name.IndexOf(':'); if (colon >= 0) name = name.Substring(colon + 1);
            return System.Text.RegularExpressions.Regex.IsMatch(name, @"QFN|DFN|SON|BGA|LGA|(?<!P)LCC|PowerPAK", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        // Parts JLC charges for one BOM line; equals need when the attrition fields are unknown (known = false)
        public static int ChargedQty(PartInfo pi, int perBoard, int boards, bool bothSides, out bool known)
        {
            int need = Math.Max(0, perBoard) * Math.Max(0, boards);
            known = pi != null && pi.Loss >= 0 && pi.LeastPatch >= 0 && pi.Reel >= 0;
            if (!known || need == 0) return need;
            int attrition = pi.Loss + (int)Math.Floor(WastageCoefficient * Math.Max(0, need - pi.Reel) + 1e-9);
            if (bothSides) attrition *= 2;
            return Math.Max(need + attrition, pi.LeastPatch);
        }

        public static AssemblyEstimate Estimate(List<BomRow> placed, Dictionary<string, PartInfo> info, int boards, bool bothSides,
            int smtJointsPerBoard, int thtJointsPerBoard, int leadlessPerBoard)
        {
            var e = new AssemblyEstimate { Boards = boards, Sides = bothSides ? 2 : 1, Standard = bothSides,
                SmtJointsPerBoard = smtJointsPerBoard, ThtJointsPerBoard = thtJointsPerBoard, LeadlessPerBoard = leadlessPerBoard };
            foreach (var r in placed)
            {
                PartInfo pi = null;
                if (Production.IsValidCode(r.Lcsc)) info.TryGetValue(r.Lcsc, out pi);
                var l = new CostLine { Row = r, Info = pi, Need = r.Qty * boards };
                bool known;
                l.Charged = ChargedQty(pi, r.Qty, boards, bothSides, out known);
                l.QuantityRuleKnown = known;
                if (pi != null) { l.Unit = pi.UnitPrice(l.Charged); l.LowStock = pi.Stock < l.Charged; }
                l.Total = double.IsNaN(l.Unit) ? 0 : l.Unit * l.Charged;
                if (double.IsNaN(l.Unit)) e.Unpriced++;
                if (pi != null && !known) e.UnknownRule++;
                e.Lines.Add(l);
            }
            e.PartsTotal = e.Lines.Sum(x => x.Total);
            // one feeder per part kind (LCSC code)
            var kinds = e.Lines.Where(x => x.Info != null).GroupBy(x => x.Row.Lcsc).Select(g => g.First().Info).ToList();
            if (e.Standard)
            {
                e.Setup = StandardSetupPerSide * e.Sides; e.Stencil = StandardStencilPerSide * e.Sides;
                e.LoadingKinds = kinds.Count; e.LoadingFeePerKind = StandardLoading;
            }
            else
            {
                e.Setup = EconomicSetup; e.Stencil = EconomicStencil;
                e.LoadingKinds = kinds.Count(k => k.Type == "Extended"); e.LoadingFeePerKind = EconomicExtendedLoading;
            }
            e.Loading = e.LoadingKinds * e.LoadingFeePerKind;
            long smt = (long)smtJointsPerBoard * boards, tht = (long)thtJointsPerBoard * boards, xray = (long)leadlessPerBoard * boards;
            e.SmtRate = Rate(e.Standard ? StandardSmtJoint : EconomicSmtJoint, smt); e.SmtCost = smt * e.SmtRate;
            e.ThtRate = Rate(ManualJoint, tht); e.ThtCost = tht * e.ThtRate;
            e.HandLabor = tht > 0 ? HandSolderingLabor : 0;
            e.XrayRate = Rate(XrayPart, xray); e.Xray = xray * e.XrayRate;
            e.Packing = e.Standard ? StandardPacking : 0;
            return e;
        }

        // Loading fee text for a JLC part type in the given assembly type
        public static double LoadingFee(string type, bool standard)
        {
            if (standard) return StandardLoading;
            return type == "Extended" ? EconomicExtendedLoading : 0;
        }
    }
}
