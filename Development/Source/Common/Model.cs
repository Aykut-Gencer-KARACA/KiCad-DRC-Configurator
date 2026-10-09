using System;
using System.Collections.Generic;
using System.Linq;

namespace KiCadDrc
{
    // ------------------------------------------------------------------ data model

    class StackLayer
    {
        public bool IsCopper;
        public string Type;       // copper / prepreg / core
        public double Thickness;
        public double Dk;
        public string Material;
    }

    class Stackup
    {
        public string Name;
        public int L;
        public double Thickness, Outer, Inner;
        public bool IsDefault;
        public List<StackLayer> Layers = new List<StackLayer>();
        public double Total { get { return Math.Round(Layers.Sum(k => k.Thickness), 4); } }
        public override string ToString() { return Name + (IsDefault ? Tx.RecommendedMark : ""); }
    }

    // What the user picked on the left panel. Mask, finish and edge are stored as language independent codes.
    class Selection
    {
        public string Maker = "JLC";
        public int Layers = 2;
        public double Thickness = 1.6, Outer = 1, Inner = 0.5, Margin = 0.05;
        public string Stackup, Mask = "green", Finish, Edge;

        public OrderedMap ToMap()
        {
            return new OrderedMap().Put("maker", Maker).Put("layers", Layers).Put("thickness", Thickness)
                .Put("outer", Outer).Put("inner", Inner).Put("stackup", Stackup).Put("mask", Mask)
                .Put("finish", Finish).Put("edge", Edge).Put("margin", Margin);
        }

        public static Selection FromMap(IDictionary<string, object> d)
        {
            var s = new Selection();
            Func<string, object> get = k => d.ContainsKey(k) ? d[k] : null;
            if (get("maker") != null) s.Maker = (string)get("maker");
            if (get("layers") != null) s.Layers = Convert.ToInt32(get("layers"));
            if (get("thickness") != null) s.Thickness = S.D(get("thickness"));
            if (get("outer") != null) s.Outer = S.D(get("outer"));
            if (get("inner") != null) s.Inner = S.D(get("inner"));
            if (get("margin") != null) s.Margin = S.D(get("margin"));
            s.Stackup = (string)get("stackup"); s.Finish = (string)get("finish"); s.Edge = (string)get("edge");
            if (get("mask") != null) s.Mask = (string)get("mask");
            return s;
        }
    }

    class RuleRow
    {
        public string Group, Name, Value, Source;
        public RuleRow(string group, string name, string value, string source) { Group = group; Name = name; Value = value; Source = source; }
    }

    class Option
    {
        public string Code, Name, KiCad; public double Value; public bool Recommended;
        public Option(string code, string name, string kicad, double value) { Code = code; Name = name; KiCad = kicad; Value = value; }
        public override string ToString() { return Name + (Recommended ? Tx.RecommendedMark : ""); }
    }

    // Everything a selection writes into KiCad
    class RuleSet
    {
        public OrderedMap Board = new OrderedMap();           // Board Setup > Constraints
        public List<string> Custom = new List<string>();      // .kicad_dru
        public double Track, Clearance, ViaDiameter = 0.6, ViaDrill = 0.3, DpWidth, DpGap, DpViaGap = 0.25;
        public double[] TrackPresets;
        public double[][] ViaPresets;
        public double HatchThickness, HatchGap, ZoneMinThickness;
        public double SilkLine = 0.15, SilkText = 1.0, SilkTextThickness = 0.17;
        public double MaskBridge;
        public bool ViaFill;
        public string FinishKiCad, MaskColor, SilkColor;
        public Stackup Stackup;
        public List<RuleRow> Rows = new List<RuleRow>();
        public List<string> Notes = new List<string>();
        public Dictionary<string, double> Limits = new Dictionary<string, double>();   // limits used by the verification test
        public double Margin;                                                          // margin actually applied (0-0.15)
    }

    interface IManufacturer
    {
        string Code { get; }
        string Name { get; }
        string Source { get; }
        int[] LayerCounts();
        double[] Thicknesses(int L);
        double[] OuterCoppers(int L, double thickness);
        double[] InnerCoppers(int L, double thickness, double outer);
        List<Stackup> Stackups(int L, double thickness, double outer, double inner);
        Option[] Masks();
        Option[] Finishes(int L, double thickness);
        Option[] Edges();
        RuleSet Calculate(Selection s);
        // Manufacturer recommendation for the given upper choices (NaN = recommend that field too)
        Selection Recommended(int L, double thickness, double outer, double inner);
        string Hint(string field);
    }
}
