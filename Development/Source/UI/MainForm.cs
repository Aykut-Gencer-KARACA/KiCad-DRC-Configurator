using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace KiCadDrc
{
    // ------------------------------------------------------------------ main window
    partial class MainForm : Form
    {
        static readonly Color Dark = Color.FromArgb(24, 32, 46), Accent = Color.FromArgb(37, 99, 235), Background = Color.FromArgb(246, 247, 249);
        static readonly Color Green = Color.FromArgb(22, 163, 74), Flash = Color.FromArgb(254, 240, 138), CopperColor = Color.FromArgb(253, 236, 214);
        static readonly Color Disabled = Color.FromArgb(156, 163, 175);
        static readonly Color InfoBack = Color.FromArgb(226, 235, 250), InfoText = Color.FromArgb(30, 58, 110);
        static readonly Color OkBack = Color.FromArgb(220, 252, 231), OkText = Color.FromArgb(21, 94, 52);
        static readonly Color WarnBack = Color.FromArgb(254, 243, 199), WarnText = Color.FromArgb(146, 64, 14);
        static readonly Color ErrorBack = Color.FromArgb(254, 226, 226), ErrorText = Color.FromArgb(153, 27, 27);

        readonly KiCadEnvironment env;
        readonly List<IManufacturer> manufacturers;
        readonly ComboBox cMaker, cLayers, cThickness, cOuter, cInner, cStackup, cMask, cFinish, cEdge;
        readonly NumericUpDown nMargin;
        readonly Label lStatus, lNote, lStackupTotal, lBanner, lDiff, lHint;
        string bannerDetail;
        bool busy;
        readonly ListView lvRules, lvStackup;
        readonly Button bApply, bProject;
        readonly Panel banner;
        System.Windows.Forms.Timer bannerTimer, buttonTimer;
        Dictionary<string, string> previousValues;
        string previousStackup;
        bool loading;
        RuleSet lastRules;
        TabControl tabs;

        // Set when the user switches the language; Program rebuilds the window with the same selection.
        // Selection and tab are captured before closing (the controls are disposed afterwards).
        public bool RestartRequested { get; private set; }
        public Selection CurrentSelection { get; private set; }
        public int CurrentTab { get; private set; }

        public MainForm(KiCadEnvironment env, List<IManufacturer> manufacturers) : this(env, manufacturers, null) { }

        public MainForm(KiCadEnvironment env, List<IManufacturer> manufacturers, Selection start)
        {
            this.env = env; this.manufacturers = manufacturers;
            restarted = start != null;
            Text = Tx.AppName;
            AutoScaleDimensions = new SizeF(96f, 96f);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 9.5f);
            ClientSize = new Size(1180, 760);
            MinimumSize = new Size(900, 640);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Background;
            Icon = AppIcon();
            DoubleBuffered = true;
            Opacity = 0;
            Shown += (o, e) =>
            {
                Anim.Play(280, t => Opacity = t, null);
                // Inconsistencies found at startup (deleted folder/template, KiCad template reset from outside...)
                if (env.Warnings.Count > 0)
                    ShowBanner("⚠  " + env.Warnings[0] + (env.Warnings.Count > 1 ? "   " + Tx.MoreWarnings(env.Warnings.Count - 1) : ""),
                        WarnBack, WarnText, env.Warnings.Count > 1 ? "•  " + string.Join("\n\n•  ", env.Warnings) : null);
            };

            // top bar
            var top = new Panel { Dock = DockStyle.Top, Height = 70, BackColor = Dark, Padding = new Padding(20, 12, 20, 10) };
            var title = new Label { Text = Tx.AppName, ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 15f), AutoSize = true, Location = new Point(18, 10) };
            lStatus = new Label { ForeColor = Color.FromArgb(170, 184, 204), AutoSize = true, Location = new Point(21, 42) };
            var signature = new Label { Text = "Aykut Gencer Karaca", ForeColor = Color.FromArgb(212, 160, 23), Font = new Font("Segoe UI Semibold", 9f), AutoSize = true };
            var version = new Label { Text = "v" + Assembly.GetExecutingAssembly().GetName().Version.ToString(2), ForeColor = Color.FromArgb(110, 124, 146), Font = new Font("Segoe UI", 8.5f), AutoSize = true };
            // Language switch: the active language is highlighted, the other one is clickable
            var langEn = LanguageLabel("EN", !Tx.Turkish);
            var langTr = LanguageLabel("TR", Tx.Turkish);
            langEn.Click += (o, e) => SwitchLanguage(false);
            langTr.Click += (o, e) => SwitchLanguage(true);
            top.Controls.Add(title); top.Controls.Add(lStatus); top.Controls.Add(signature); top.Controls.Add(version);
            top.Controls.Add(langEn); top.Controls.Add(langTr);
            top.Layout += (o, e) =>
            {
                signature.Location = new Point(top.ClientSize.Width - signature.Width - 20, 14);
                langTr.Location = new Point(top.ClientSize.Width - langTr.Width - 20, 36);
                langEn.Location = new Point(langTr.Left - langEn.Width - 2, 36);
                version.Location = new Point(langEn.Left - version.Width - 10, 37);
            };

            // banner (slides down after Apply / Restore defaults)
            banner = new Panel { Dock = DockStyle.Top, Height = 0, Visible = false, Padding = new Padding(22, 0, 10, 0) };
            lBanner = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 9.5f) };
            var bCloseBanner = new Label { Text = "✕", Dock = DockStyle.Right, Width = 30, TextAlign = ContentAlignment.MiddleCenter, Cursor = Cursors.Hand };
            bCloseBanner.Click += (o, e) => HideBanner();
            lBanner.Click += (o, e) => { if (bannerDetail != null) Dialogs.Show(this, bannerDetail, Tx.Details, MessageBoxIcon.Information); };
            banner.Controls.Add(lBanner); banner.Controls.Add(bCloseBanner);

            // bottom bar
            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 60, BackColor = Color.White, Padding = new Padding(16, 12, 16, 12) };
            bottom.Paint += (o, e) => { using (var pen = new Pen(Color.FromArgb(225, 228, 233))) e.Graphics.DrawLine(pen, 0, 0, bottom.Width, 0); };
            bApply = MakeButton(Tx.Apply, true); bApply.Dock = DockStyle.Right; bApply.Width = 130;
            var bClose = MakeButton(Tx.Close, false); bClose.Dock = DockStyle.Right; bClose.Width = 100;
            var bDefault = MakeButton(Tx.RestoreDefaults, false); bDefault.Dock = DockStyle.Left; bDefault.Width = 150;
            var bHelp = MakeButton(Tx.HowToUse, false); bHelp.Dock = DockStyle.Left; bHelp.Width = 150;
            var lData = new LinkLabel { Text = Tx.DataFolderLink, Dock = DockStyle.Left, AutoSize = false, Width = 110, TextAlign = ContentAlignment.MiddleCenter, LinkColor = Color.FromArgb(75, 85, 99) };
            lData.LinkClicked += (o, e) => { using (System.Diagnostics.Process.Start("explorer.exe", "\"" + env.DataDir + "\"")) { } };
            // Dock: controls added later sit closer to the edge -> right [Apply][Close], left [How to use][Restore defaults][Data folder]
            bottom.Controls.Add(bApply); bottom.Controls.Add(Spacer(DockStyle.Right)); bottom.Controls.Add(bClose);
            bottom.Controls.Add(lData); bottom.Controls.Add(bDefault); bottom.Controls.Add(Spacer(DockStyle.Left)); bottom.Controls.Add(bHelp);
            bApply.Click += (o, e) => Apply();
            bDefault.Click += (o, e) => RestoreDefaults();
            bClose.Click += (o, e) => Close();
            bHelp.Click += (o, e) => Help();
            AcceptButton = bApply; CancelButton = bClose;

            // left: choices
            var left = new TableLayoutPanel { Dock = DockStyle.Left, Width = 405, ColumnCount = 2, Padding = new Padding(18, 16, 12, 10), BackColor = Background };
            left.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
            left.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            cMaker = Combo(left, Tx.Manufacturer); cLayers = Combo(left, Tx.Layers); cThickness = Combo(left, Tx.Thickness);
            cOuter = Combo(left, Tx.OuterCopper); cInner = Combo(left, Tx.InnerCopper); cStackup = Combo(left, "Stackup");
            cMask = Combo(left, Tx.MaskColor); cFinish = Combo(left, Tx.Finish); cEdge = Combo(left, Tx.BoardEdge);
            nMargin = new NumericUpDown { DecimalPlaces = 2, Increment = 0.01m, Minimum = 0, Maximum = 0.15m, Value = 0.05m, Dock = DockStyle.Fill, Margin = new Padding(0, 4, 0, 4) };
            FieldLabel(left, Tx.SafetyMargin); left.Controls.Add(nMargin);
            var marginNote = new Label { Text = Tx.SafetyMarginNote, ForeColor = Color.Gray, AutoSize = true, Margin = new Padding(0, 0, 0, 6) };
            left.Controls.Add(new Label()); left.Controls.Add(marginNote);
            var bRecommended = MakeButton("★  " + Tx.SelectRecommended, false); bRecommended.Dock = DockStyle.Fill; bRecommended.Height = 34; bRecommended.Margin = new Padding(0, 2, 0, 2);
            bRecommended.Click += (o, e) => SelectRecommended();
            left.Controls.Add(bRecommended); left.SetColumnSpan(bRecommended, 2);
            bProject = MakeButton("⇄  " + Tx.ApplyToProject, false); bProject.Dock = DockStyle.Fill; bProject.Height = 34; bProject.Margin = new Padding(0, 2, 0, 2);
            bProject.Click += (o, e) => ApplyToProject();
            left.Controls.Add(bProject); left.SetColumnSpan(bProject, 2);
            lDiff = new Label { AutoSize = true, MaximumSize = new Size(340, 0), Margin = new Padding(0, 6, 0, 0) };
            left.Controls.Add(lDiff); left.SetColumnSpan(lDiff, 2);
            lHint = new Label { AutoSize = true, MaximumSize = new Size(340, 0), ForeColor = InfoText, BackColor = InfoBack, Padding = new Padding(8), Margin = new Padding(0, 8, 0, 0), Visible = false };
            left.Controls.Add(lHint); left.SetColumnSpan(lHint, 2);
            lNote = new Label { AutoSize = true, MaximumSize = new Size(340, 0), ForeColor = WarnText, BackColor = WarnBack, Padding = new Padding(8), Margin = new Padding(0, 8, 0, 0), Visible = false };
            left.Controls.Add(lNote); left.SetColumnSpan(lNote, 2);
            left.AutoScroll = true;

            // Hovering / focusing a field shows its explanation
            var fields = new Dictionary<Control, string> {
                { cLayers, "layers" }, { cThickness, "thickness" }, { cOuter, "outer" }, { cInner, "inner" }, { cStackup, "stackup" },
                { cMask, "mask" }, { cFinish, "finish" }, { cEdge, "edge" }, { nMargin, "margin" } };
            foreach (var field in fields)
            {
                string key = field.Value;
                field.Key.Enter += (o, e) => ShowHint(key);
                field.Key.MouseEnter += (o, e) => ShowHint(key);
            }

            // right: tabs
            tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(14, 5) };
            var tRules = new TabPage(Tx.TabRules) { BackColor = Color.White, Padding = new Padding(6) };
            var tStack = new TabPage("Stackup") { BackColor = Color.White, Padding = new Padding(6) };
            lvRules = MakeList(new[] { Tx.ColRule, Tx.ColValue, Tx.ColSource }, new[] { 200, 185, 360 });
            lvStackup = MakeList(new[] { Tx.ColLayer, Tx.ColType, Tx.ColThickness, "Dk", Tx.ColMaterial }, new[] { 110, 90, 90, 60, 160 });
            lStackupTotal = new Label { Dock = DockStyle.Bottom, Height = 28, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.DimGray };
            tRules.Controls.Add(lvRules);
            tStack.Controls.Add(lvStackup); tStack.Controls.Add(lStackupTotal);
            tabs.TabPages.Add(tRules); tabs.TabPages.Add(tStack); tabs.TabPages.Add(ProductionTab()); tabs.TabPages.Add(CalculatorsTab());
            var right = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4, 14, 16, 12), BackColor = Background };
            right.Controls.Add(tabs);

            // Dock order: added later is placed first -> top bar, banner, bottom bar, left, right
            Controls.Add(right); Controls.Add(left); Controls.Add(bottom); Controls.Add(banner); Controls.Add(top);
            // The calculators use the whole width: the settings panel is hidden while their tab is open
            settingsPanel = left;
            // The bottom bar (Apply, Restore defaults, Close…) belongs to the rule settings: shown only on the rules and stackup tabs.
            // The calculators also hide the settings panel to use the whole width.
            Action updateChrome = () =>
            {
                settingsPanel.Visible = tabs.SelectedTab != calcPage;
                bottom.Visible = tabs.SelectedIndex <= 1;
            };
            tabs.SelectedIndexChanged += (o, e) => updateChrome();

            foreach (var m in manufacturers) cMaker.Items.Add(m.Name);
            // Changing the layer count resets everything that depends on it to the recommendation (mask, edge, margin are kept)
            cLayers.SelectedIndexChanged += (o, e) => { if (!loading) LayersChanged(); };
            foreach (var c in new[] { cMaker, cThickness, cOuter, cInner }) c.SelectedIndexChanged += (o, e) => { if (!loading) Fill(); };
            foreach (var c in new[] { cStackup, cMask, cFinish, cEdge }) c.SelectedIndexChanged += (o, e) => { if (!loading) Recalculate(); };
            nMargin.ValueChanged += (o, e) => { if (!loading) Recalculate(); };

            Start(start);
        }

        // True when this window replaces one closed for a language switch (a start selection was handed over)
        bool restarted;

        public void SelectTab(int i) { if (i >= 0 && i < tabs.TabCount) tabs.SelectedIndex = i; }

        Label LanguageLabel(string text, bool active)
        {
            return new Label
            {
                Text = text, AutoSize = true, Padding = new Padding(3, 1, 3, 1),
                Font = new Font("Segoe UI", 8.5f, active ? FontStyle.Bold : FontStyle.Regular),
                ForeColor = active ? Color.FromArgb(212, 160, 23) : Color.FromArgb(150, 164, 186),
                Cursor = active ? Cursors.Default : Cursors.Hand
            };
        }

        void SwitchLanguage(bool turkish)
        {
            if (turkish == Tx.Turkish) return;
            if (busy) { Dialogs.Show(this, Tx.WaitForOperation, Tx.AppName, MessageBoxIcon.Information); return; }
            CurrentSelection = Collect(); CurrentTab = tabs.SelectedIndex;
            Tx.Turkish = turkish;
            try { env.SetSetting(KiCadEnvironment.SettingLanguage, turkish ? "tr" : "en"); } catch (Exception) { }
            RestartRequested = true;
            Close();
        }
    }
}
