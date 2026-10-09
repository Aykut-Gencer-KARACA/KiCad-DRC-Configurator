using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace KiCadDrc
{
    // ------------------------------------------------------------------ PCB calculators
    // Every calculator is a class with a title, a description ("what is it, how is it calculated") and a view.
    // The math of each calculator lives in its own static *Math class so it can be unit tested without the window.

    // What a calculator can use from the window: the unit system and the currently selected stackup/rules
    class CalcHost
    {
        public bool Metric = true;
        public Func<RuleSet> Rules = () => null;
        public event Action UnitsChanged;
        // raised when the selection on the left (stackup, copper...) changed while the calculator view is kept
        public event Action<RuleSet> RulesChanged;
        public void SetMetric(bool metric) { Metric = metric; if (UnitsChanged != null) UnitsChanged(); }
        public void NotifyRules(RuleSet rules) { if (RulesChanged != null) RulesChanged(rules); }
    }

    abstract class Calculator
    {
        public abstract string Id { get; }
        public abstract string Title { get; }
        public abstract string Description { get; }
        // One line under the title: what the calculator answers
        public virtual string Summary { get { return ""; } }
        public virtual bool Ready { get { return true; } }
        // Builds the calculator's inputs and results (once per window; the view keeps its inputs)
        public abstract Control Build(CalcHost host);
        // "How it works" page: explanation with figures, formulas and an example
        public virtual Control BuildGuide(CalcHost host) { return new CalcDoc().Para(Description).Root; }
    }

    // Calculator that is listed but not written yet (the list shows the whole plan)
    class PlannedCalculator : Calculator
    {
        readonly string id;
        public PlannedCalculator(string id) { this.id = id; }
        public override string Id { get { return id; } }
        public override string Title { get { return Tx.CalcTitle(id); } }
        public override string Description { get { return Tx.CalcPlannedDescription; } }
        public override bool Ready { get { return false; } }
        public override Control Build(CalcHost host)
        {
            return new Label { Text = Tx.CalcComingSoon, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.DimGray };
        }
    }

    static class CalculatorList
    {
        // Same order as Saturn PCB Toolkit's tabs
        public static List<Calculator> All()
        {
            return new List<Calculator>
            {
                new BandwidthCalculator(),
                new PlannedCalculator("impedance"), new PlannedCalculator("conductor"), new PlannedCalculator("conversion"),
                new PlannedCalculator("diffpair"), new PlannedCalculator("embedded_resistor"), new PlannedCalculator("er_effective"),
                new PlannedCalculator("fusing"), new PlannedCalculator("mechanical"), new PlannedCalculator("spacing"),
                new PlannedCalculator("ohms_law"), new PlannedCalculator("padstack"), new PlannedCalculator("pdn"),
                new PlannedCalculator("planar_inductor"), new PlannedCalculator("ppm_xtal"), new PlannedCalculator("thermal"),
                new PlannedCalculator("via"), new PlannedCalculator("wavelength"), new PlannedCalculator("reactance"),
            };
        }
    }
}
