using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace KiCadDrc
{
    // Bandwidth & maximum conductor length. Clone of Saturn PCB Toolkit's first tab (rise time or frequency input, substrate
    // list, microstrip/stripline, Sr factor, lambda divisor, units) plus: live results and drawings, Er from the selected JLC
    // stackup, propagation delay, wavelength on the trace and a check of a given trace length.
    class BandwidthCalculator : Calculator
    {
        public override string Id { get { return "bandwidth"; } }
        public override string Title { get { return Tx.CalcTitle(Id); } }
        public override string Summary { get { return Tx.BwSummary; } }
        public override string Description { get { return Tx.BandwidthDescription; } }

        public override Control Build(CalcHost host)
        {
            TableLayoutPanel left, right;
            var root = CalcUi.Columns(out left, out right);
            var state = new BandwidthState();
            Action recalc = null;
            Action changed = () => { if (recalc != null) recalc(); };

            // ---- input
            var gIn = CalcUi.Group(left, Tx.BwInput);
            var mode = CalcUi.Radios(gIn, Tx.BwInputMethod, new[] { Tx.BwRiseTime, Tx.BwFrequency }, 0, changed);
            var rise = CalcUi.Number("1", 1e-6, 1e9, changed);
            var riseUnit = CalcUi.Choice(new object[] { "ps", "ns", "µs" }, 1, changed);
            var riseLabel = CalcUi.Row(gIn, Tx.BwRiseTime, CalcUi.WithUnit(rise, riseUnit));
            var freq = CalcUi.Number("100", 1e-6, 1e12, changed);
            var freqUnit = CalcUi.Choice(new object[] { "Hz", "kHz", "MHz", "GHz" }, 2, changed);
            var freqLabel = CalcUi.Row(gIn, Tx.BwFrequency, CalcUi.WithUnit(freq, freqUnit));

            // ---- dielectric (with the live cross-section)
            var gDiel = CalcUi.Group(left, Tx.BwDielectric);
            var material = CalcUi.Choice(new object[0], 0, changed);
            CalcUi.Row(gDiel, Tx.BwMaterial, material);
            var er = CalcUi.Number("4.6", 1, 100, changed);
            CalcUi.Row(gDiel, "Er (Dk)", er);
            var tg = new Label { AutoSize = true, ForeColor = CalcUi.NoteText };
            CalcUi.Row(gDiel, "Tg", tg);
            var circuit = CalcUi.Radios(gDiel, Tx.BwCircuit, new[] { "Microstrip", "Stripline" }, 0, changed);
            var section = CalcUi.Figure(gDiel, 150, (g, r) => BandwidthFigures.CrossSection(g, r, state));
            // materials follow the selected stackup; the choice is kept when the list is refreshed
            Action<RuleSet> fillMaterials = rules =>
            {
                var current = material.SelectedItem as Material;
                var list = Materials.WithStackup(rules == null ? null : rules.Stackup);
                material.Items.Clear();
                foreach (var m in list) material.Items.Add(m);
                int keep = current == null ? -1 : list.FindIndex(m => m.Name == current.Name || (m.Note != null && m.Note == current.Note));
                material.SelectedIndex = keep >= 0 ? keep : 0;
            };
            fillMaterials(host.Rules());
            host.RulesChanged += fillMaterials;

            // ---- method settings and optional trace check
            var gSet = CalcUi.Group(left, Tx.BwSettings);
            var sr = CalcUi.Choice(BandwidthMath.SrFactors.Select(x => (object)(S.F(x) + " Sr")), 1, changed);
            CalcUi.Row(gSet, Tx.BwSrFactor, sr);
            var lambda = CalcUi.Choice(BandwidthMath.LambdaDivisors.Select(x => (object)("λ / " + x)), 1, changed);
            CalcUi.Row(gSet, Tx.BwLambdaDivisor, lambda);
            var trace = CalcUi.Number("", 0, 1e6, changed);
            var traceUnit = CalcUi.Choice(new object[] { "mm", "in" }, host.Metric ? 0 : 1, changed);
            CalcUi.Row(gSet, Tx.BwTraceLength, CalcUi.WithUnit(trace, traceUnit));
            CalcUi.Note(gSet, Tx.BwTraceNote);

            // ---- results: the picture first, then the numbers
            var gPic = CalcUi.Group(right, Tx.BwPicture);
            var picture = CalcUi.Figure(gPic, 180, (g, r) => BandwidthFigures.TraceScale(g, r, state));
            var verdict = CalcUi.Note(gPic, "");
            verdict.Font = new Font("Segoe UI Semibold", 9.5f);
            var gIpc = CalcUi.Group(right, Tx.BwIpcMethod);
            TextBox rBandwidth;
            var bandwidthLabel = CalcUi.ResultRow(gIpc, Tx.BwBandwidth, out rBandwidth);
            var rErEff = CalcUi.Result(gIpc, Tx.BwErEff);
            var rSpeed = CalcUi.Result(gIpc, Tx.BwSpeed);
            var rDelay = CalcUi.Result(gIpc, Tx.BwDelay);
            var rRiseDist = CalcUi.Result(gIpc, Tx.BwRiseDistance);
            var rMaxIpc = CalcUi.Result(gIpc, Tx.BwMaxLength);
            var ipcNote = CalcUi.Note(gIpc, "");
            var gFreq = CalcUi.Group(right, Tx.BwFrequencyMethod);
            var rLambda = CalcUi.Result(gFreq, Tx.BwWavelengthAir);
            var rLambdaIn = CalcUi.Result(gFreq, Tx.BwWavelengthDielectric);
            var rMaxFreq = CalcUi.Result(gFreq, Tx.BwMaxLength);
            CalcUi.Note(gFreq, Tx.BwFrequencyNote);

            recalc = () =>
            {
                var m = material.SelectedItem as Material;
                if (m == null) return;
                bool byRise = mode[0].Checked;
                rise.Enabled = riseUnit.Enabled = riseLabel.Enabled = byRise;
                freq.Enabled = freqUnit.Enabled = freqLabel.Enabled = !byRise;
                er.ReadOnly = !m.Editable;
                if (!m.Editable && CalcUi.Parse(er.Text) != m.Er) { er.Text = S.F(m.Er); return; }   // TextChanged calls recalc again
                tg.Text = double.IsNaN(m.Tg) ? "—" : S.F(m.Tg) + " °C";

                double riseScale = new[] { 1e-12, 1e-9, 1e-6 }[Math.Max(0, riseUnit.SelectedIndex)];
                double freqScale = new[] { 1.0, 1e3, 1e6, 1e9 }[Math.Max(0, freqUnit.SelectedIndex)];
                double tr = byRise ? CalcUi.Value(rise) * riseScale : BandwidthMath.RiseFromFrequency(CalcUi.Value(freq) * freqScale);
                double f = byRise ? BandwidthMath.BandwidthFromRise(tr) : CalcUi.Value(freq) * freqScale;
                double erValue = CalcUi.Value(er);
                bool micro = circuit[0].Checked;
                double erEff = BandwidthMath.ErEffective(erValue, micro);
                double factor = BandwidthMath.SrFactors[Math.Max(0, sr.SelectedIndex)];
                int divisor = BandwidthMath.LambdaDivisors[Math.Max(0, lambda.SelectedIndex)];
                bool metric = host.Metric;

                // in frequency mode the first row shows the equivalent rise time instead of the bandwidth
                bandwidthLabel.Text = byRise ? Tx.BwBandwidth : Tx.BwEquivalentRise;
                rBandwidth.Text = byRise ? CalcUi.Frequency(f) : CalcUi.Time(tr);
                rErEff.Text = CalcUi.Sig(erEff, 4) + (micro ? "  (0.475·Er + 0.67)" : "  (= Er)");
                double v = BandwidthMath.Speed(erEff);
                rSpeed.Text = double.IsNaN(v) ? "—" : CalcUi.Sig(v / 1e6, 5) + " Mm/s  (" + CalcUi.Sig(100 / Math.Sqrt(erEff), 3) + " % c)";
                double delay = BandwidthMath.DelayPerMetre(erEff);
                rDelay.Text = double.IsNaN(delay) ? "—" : CalcUi.Sig(delay * 1e9, 4) + " ps/mm  (" + CalcUi.Sig(delay * 0.0254 * 1e12, 4) + " ps/in)";
                double sDist = BandwidthMath.RiseDistance(tr, erEff);
                rRiseDist.Text = CalcUi.Length(sDist, metric);
                double lIpc = BandwidthMath.MaxLengthIpc(tr, erEff, factor);
                rMaxIpc.Text = CalcUi.Length(lIpc, metric);
                ipcNote.Text = byRise ? Tx.BwIpcNote(S.F(factor)) : Tx.BwIpcNoteFromFrequency;

                rLambda.Text = CalcUi.Length(BandwidthMath.WavelengthAir(f), metric);
                rLambdaIn.Text = CalcUi.Length(BandwidthMath.WavelengthIn(f, erEff), metric);
                double lFreq = BandwidthMath.MaxLengthFrequency(f, divisor);
                rMaxFreq.Text = CalcUi.Length(lFreq, metric);

                double traceM = string.IsNullOrWhiteSpace(trace.Text) ? double.NaN : CalcUi.Value(trace) * (traceUnit.SelectedIndex == 1 ? 0.0254 : 0.001);
                double limit = Math.Min(lIpc, lFreq);
                if (double.IsNaN(limit)) { verdict.Text = ""; }
                else if (double.IsNaN(traceM)) { verdict.Text = Tx.BwVerdictNone(CalcUi.Length(limit, metric)); verdict.ForeColor = CalcUi.NoteText; }
                else if (traceM <= limit) { verdict.Text = "✓  " + Tx.BwVerdictShort(CalcUi.Length(traceM, metric), CalcUi.Length(limit, metric)); verdict.ForeColor = CalcUi.GoodText; }
                else { verdict.Text = "⚠  " + Tx.BwVerdictLong(CalcUi.Length(traceM, metric), CalcUi.Length(limit, metric)); verdict.ForeColor = CalcUi.WarnText; }

                state.Microstrip = micro; state.Metric = metric; state.ByRise = byRise; state.Er = erValue; state.ErEff = erEff;
                state.Sr = sDist; state.LIpc = lIpc; state.LFreq = lFreq; state.Trace = traceM; state.Factor = factor; state.Divisor = divisor;
                section.Invalidate(); picture.Invalidate();
            };
            host.UnitsChanged += () => { if (!root.IsDisposed) recalc(); };
            recalc();
            return root;
        }

        // ---- "How it works" page
        public override Control BuildGuide(CalcHost host)
        {
            var d = new CalcDoc();
            var sample = new BandwidthState { Microstrip = true, Er = 4.6, ErEff = BandwidthMath.ErEffective(4.6, true) };
            var sampleStrip = new BandwidthState { Microstrip = false, Er = 4.6, ErEff = 4.6 };
            d.Callout(Tx.GuideInShort, Tx.BwGuideInShort, Color.FromArgb(220, 252, 231), Color.FromArgb(21, 94, 52))
             .Heading(Tx.GuideWhat).Para(Tx.BwGuideWhat).Para(Tx.BwGuideAnalogy)
             .Heading(Tx.BwGuideProblemTitle).Para(Tx.BwGuideProblem)
             .Picture(new Figure(230, (g, r) => BandwidthFigures.ReceiverWave(g, r, new[] { 20.0, 200.0 },
                 new[] { Fig.Good, Fig.Bad }, new[] { Tx.FigShortTrace("20 mm"), Tx.FigLongTrace("200 mm") })), Tx.BwGuideFigProblem)
             .Heading(Tx.GuideTry).Para(Tx.BwGuideTry)
             .Embed(TryItYourself())
             .Heading(Tx.GuideWhen).Bullets(Tx.BwGuideWhen)
             .Heading(Tx.GuideHow)
             .Step(1, Tx.BwGuideStep1Title, Tx.BwGuideStep1)
             .Picture(new Figure(150, (g, r) => BandwidthFigures.EdgeWaveform(g, r)), Tx.BwGuideFig1)
             .Formula("f = 0.35 / tr", Tx.BwGuideFormula1Note)
             .Step(2, Tx.BwGuideStep2Title, Tx.BwGuideStep2)
             .Picture(new Figure(150, (g, r) => BandwidthFigures.CrossSection(g, r, sample)), Tx.BwGuideFig2a)
             .Picture(new Figure(150, (g, r) => BandwidthFigures.CrossSection(g, r, sampleStrip)), Tx.BwGuideFig2b)
             .Formula("v = c / √Er_eff", Tx.BwGuideFormula2Note, "Er_eff = Er                    (stripline)", "Er_eff = 0.475 · Er + 0.67     (microstrip)")
             .Step(3, Tx.BwGuideStep3Title, Tx.BwGuideStep3)
             .Formula("Sr = tr · v", "L_max = k · Sr        (k = 0.25)", Tx.BwGuideFormula3Note)
             .Step(4, Tx.BwGuideStep4Title, Tx.BwGuideStep4)
             .Picture(new Figure(140, (g, r) => BandwidthFigures.Wavelength(g, r)), Tx.BwGuideFig4)
             .Formula("λ = c / f", "L_max = λ / n        (n = 7)")
             .Callout(Tx.GuideExample, Tx.BwGuideExample, Color.FromArgb(239, 244, 255), Color.FromArgb(30, 58, 110))
             .Heading(Tx.GuideUse).Bullets(Tx.BwGuideUse)
             .Heading(Tx.GuideFaq);
            foreach (var qa in Tx.BwGuideFaq) d.Qa(qa[0], qa[1]);
            d.Heading(Tx.GuideTerms).Terms(Tx.BwGuideTerms)
             .Callout(Tx.GuideNotes, Tx.BwGuideNotes, Color.FromArgb(254, 243, 199), Color.FromArgb(146, 64, 14));
            return d.Root;
        }

        // Interactive demo: drag the trace length and watch the receiver's voltage (tr = 1 ns, FR-4 microstrip, 15 ohm driver)
        static Control TryItYourself()
        {
            double critical = BandwidthMath.MaxLengthIpc(BandwidthFigures.DemoRise, BandwidthMath.ErEffective(4.6, true), 0.25) * 1000;
            var panel = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Color.FromArgb(248, 250, 252), Padding = new Padding(10) };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var info = new Label { AutoSize = true, Font = new Font("Segoe UI Semibold", 10f), ForeColor = Color.FromArgb(31, 41, 55), Margin = new Padding(0, 0, 0, 2) };
            var slider = new TrackBar { Minimum = 1, Maximum = 60, Value = 4, TickFrequency = 2, SmallChange = 1, LargeChange = 4, Dock = DockStyle.Fill, Height = CalcUi.Px(40) };
            double length = 20;
            var figure = new Figure(230, (g, r) => BandwidthFigures.ReceiverWave(g, r, new[] { length }, new[] { length <= critical ? Fig.Good : Fig.Bad }, null)) { Dock = DockStyle.Fill };
            var verdict = new Label { AutoSize = true, Font = new Font("Segoe UI Semibold", 9.75f), Margin = new Padding(0, 4, 0, 0) };
            var what = new Label { AutoSize = true, Font = new Font("Segoe UI", 9.5f), ForeColor = Color.FromArgb(55, 65, 81), Margin = new Padding(0, 2, 0, 0) };
            Action update = () =>
            {
                length = slider.Value * 5;
                double td = length / 1000 / BandwidthFigures.DemoSpeed;
                double over = ReflectionModel.OvershootPercent(BandwidthFigures.DemoRise, td);
                info.Text = Tx.BwTryInfo(S.F(length), CalcUi.Sig(2 * td * 1e9, 3), S.F(Math.Round(critical)));
                bool ok = length <= critical;
                verdict.ForeColor = ok ? CalcUi.GoodText : CalcUi.WarnText;
                verdict.Text = (ok ? "✓  " : "⚠  ") + (ok ? Tx.BwTryClean(S.F(Math.Round(over))) : Tx.BwTryRinging(S.F(Math.Round(over))));
                what.Text = ok ? Tx.BwTryWhyClean : Tx.BwTryWhyRinging;
                figure.Invalidate();
            };
            slider.ValueChanged += (o, e) => update();
            panel.Controls.Add(info); panel.Controls.Add(slider); panel.Controls.Add(figure); panel.Controls.Add(verdict); panel.Controls.Add(what);
            update();
            return panel;
        }
    }
}