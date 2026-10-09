using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace KiCadDrc
{
    // ------------------------------------------------------------------ production files (BOM, CPL, gerber, XLSX)
    // The BOM is read from the schematic itself (kicad-cli sch export bom): every placed part comes, including passives
    // from KiCad's own library. LCSC codes come from the symbols' "LCSC Part" field in the schematic; for parts without
    // one the user can enter a code in the app, kept in <project>.lcsc.json in the project folder (the schematic is not
    // written by the app). Outputs go to the production folder inside the project folder.

    class BomRow
    {
        public string Category, Refs, Value, Footprint, Lcsc;
        public int Qty;
        public bool Excluded;   // "Place" unticked (solder pad, hand-mounted part): not in the JLC BOM/CPL, no code needed
        public bool FromSchematic;   // the code comes from the schematic's "LCSC Part" field (changed in KiCad, not in the app)
        public string Key { get { return Value + "|" + Footprint; } }   // key the code is stored under (survives renumbering)
        public string Package { get { int i = Footprint.IndexOf(':'); return i >= 0 ? Footprint.Substring(i + 1) : Footprint; } }
    }

    class CplRow
    {
        public string Ref, Value, Package, Side;
        public string Footprint;   // "library:footprint" from the board (set by JlcPlacement.Adjust)
        public double X, Y, Rotation;
    }

    // Part information from the JLC parts library (queried by LCSC code)
    class PartInfo
    {
        public string Manufacturer, Mpn, Package, Description, Type, Datasheet;   // Type: Basic / Extended / Preferred
        public long Stock;
        public List<double[]> Prices = new List<double[]>();                     // {from qty, to qty (-1 = open), unit price $}
        // JLC's assembly quantity rule (part page fields lossNumber, leastPatchNumber, encapsulationNumber; -1 = not known,
        // e.g. an old cache entry): extra parts for attrition, minimum quantity charged, reel size
        public int Loss = -1, LeastPatch = -1, Reel = -1;
        public double UnitPrice(long qty)
        {
            var tier = Prices.FirstOrDefault(f => qty >= f[0] && (f[1] < 0 || qty <= f[1]))
                       // above every closed tier: the highest tier that starts below the quantity; below all: the first tier
                       ?? Prices.Where(f => f[0] <= qty).OrderByDescending(f => f[0]).FirstOrDefault()
                       ?? Prices.OrderBy(f => f[0]).FirstOrDefault();
            return tier == null ? double.NaN : tier[2];
        }
    }
}
