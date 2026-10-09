using System;
using System.Collections.Generic;
using System.Linq;

namespace KiCadDrc
{
    // Substrate materials with their nominal dielectric constant (Er at ~1 GHz) and glass transition temperature.
    // Values as listed by the manufacturers' datasheets (the same set Saturn PCB Toolkit offers).
    class Material
    {
        public string Name; public double Er; public double Tg;   // Tg in degC, NaN = not applicable (PTFE, ceramic filled)
        public bool Editable;                                       // "Custom": Er typed by the user
        public string Note;                                         // e.g. which stackup layer it came from
        public override string ToString() { return Name; }
    }

    static class Materials
    {
        static Material M(string n, double er, double tg) { return new Material { Name = n, Er = er, Tg = tg }; }

        public static List<Material> Standard()
        {
            double na = double.NaN;
            return new List<Material>
            {
                M("FR-4 STD", 4.6, 130), M("FR-5", 4.3, 170), M("FR406", 4.6, 170), M("FR408", 3.8, 180),
                M("Getek ML200C", 3.8, 175), M("Getek ML200D", 3.9, 175), M("Getek ML200M", 3.8, 175), M("Getek RG200D", 4.2, 175),
                M("Isola P95", 3.78, 260), M("Isola P96", 3.78, 260), M("Isola P26N", 3.9, 250),
                M("RO2800", 2.94, na), M("RO3003", 3.0, na), M("RO3006", 6.15, na), M("RO3010", 10.2, na),
                M("RO4003", 3.38, 280), M("RO4350B", 3.66, 280),
                M("RT5500", 2.5, 260), M("RT5870", 2.35, 260), M("RT5880", 2.2, 260),
                M("RT6002", 2.94, na), M("RT6006", 6.15, na), M("RT6010", 10.2, na),
                M("Teflon PTFE", 2.1, 240), M("Arlon 25N", 3.38, 260), M("Arlon 33N", 4.25, 250), M("Arlon 85N", 4.2, 250),
                M("PCL-FR-226", 4.5, 140), M("PCL-FR-240", 4.5, 140), M("PCL-FR-370", 4.5, 175), M("PCL-FR-370HR", 4.24, 180),
                M("N4000-7 EF", 4.1, 165), M("N4000-13", 3.7, 210), M("N4000-13SI", 3.4, 210), M("N4000-13 EP", 3.7, 210),
                M("N4000-13 EPSI", 3.4, 210), M("N4000-29", 4.5, 185), M("N7000-1", 3.9, 260),
                M("Ventec VT-47", 4.6, 180), M("Ventec VT-901", 4.15, 250), M("Ventec VT-90H", 4.15, 250),
                M("Megtron6", 3.4, 185), M("Kappa 438", 4.38, 280), M("Kapton", 3.4, 400), M("Air", 1.0, na),
            };
        }

        // Materials of the selected JLC stackup first (outer dielectric for microstrip, inner for stripline), then the
        // standard list, then "Custom"
        public static List<Material> WithStackup(Stackup st)
        {
            var list = new List<Material>();
            if (st != null)
            {
                var dielectrics = st.Layers.Where(l => !l.IsCopper).ToList();
                if (dielectrics.Count > 0)
                {
                    var outer = dielectrics[0];
                    list.Add(new Material { Name = Tx.CalcStackupOuter(st.Name, outer.Material, S.F(outer.Dk)), Er = outer.Dk, Tg = double.NaN, Note = "outer" });
                    if (dielectrics.Count > 2)
                    {
                        // inner dielectrics: everything except the two outermost layers (weighted by thickness)
                        var inner = dielectrics.Skip(1).Take(dielectrics.Count - 2).ToList();
                        double dk = Math.Round(inner.Sum(l => l.Dk * l.Thickness) / inner.Sum(l => l.Thickness), 2);
                        list.Add(new Material { Name = Tx.CalcStackupInner(st.Name, S.F(dk)), Er = dk, Tg = double.NaN, Note = "inner" });
                    }
                }
            }
            list.AddRange(Standard());
            list.Add(new Material { Name = Tx.CalcCustomMaterial, Er = 4.6, Tg = double.NaN, Editable = true });
            return list;
        }
    }
}
