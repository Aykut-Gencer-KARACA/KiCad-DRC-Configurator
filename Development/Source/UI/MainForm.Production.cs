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
    partial class MainForm
    {
        // ---------------------------------------------------------- "Project and production" tab
        ProjectInfo selectedProject;
        List<BomRow> bom;
        DataGridView gridBom;
        Label lProject, lProjectSub, lBomStatus, lBomEmpty;
        Button bBuild, bImport, bRefresh;
        LinkLabel lOutputFolder;
        NumericUpDown nBoards;
        bool fillingBom;

        TabPage ProductionTab()
        {
            var page = new TabPage(Tx.TabProduction) { BackColor = Color.White, Padding = new Padding(8) };

            var top = new Panel { Dock = DockStyle.Top, Height = 58 };
            lProject = new Label { Text = Tx.NoProjectSelected, Font = new Font("Segoe UI Semibold", 11f), AutoSize = true, Location = new Point(4, 4) };
            lProjectSub = new Label { Text = Tx.SelectProjectHint, ForeColor = Color.DimGray, AutoSize = true, Location = new Point(5, 30) };
            var bSelect = MakeButton(Tx.SelectProject, false); bSelect.Size = new Size(120, 32); bSelect.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            bRefresh = MakeButton("↻  " + Tx.Refresh, false); bRefresh.Size = new Size(100, 32); bRefresh.Anchor = AnchorStyles.Top | AnchorStyles.Right; bRefresh.Enabled = false;
            top.Controls.Add(lProject); top.Controls.Add(lProjectSub); top.Controls.Add(bSelect); top.Controls.Add(bRefresh);
            top.Layout += (o, e) => { bSelect.Location = new Point(top.ClientSize.Width - bSelect.Width - 4, 8); bRefresh.Location = new Point(bSelect.Left - bRefresh.Width - 6, 8); };
            bSelect.Click += (o, e) =>
            {
                using (var d = new OpenFileDialog { Title = Tx.SelectKiCadProject, Filter = Tx.KiCadProjectFilter, RestoreDirectory = true })
                    if (d.ShowDialog(this) == DialogResult.OK) SelectProject(d.FileName);
            };
            bRefresh.Click += (o, e) => { if (selectedProject != null) SelectProject(selectedProject.ProPath); };

            gridBom = new DataGridView
            {
                Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false,
                RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None, SelectionMode = DataGridViewSelectionMode.CellSelect, EditMode = DataGridViewEditMode.EditOnEnter,
                GridColor = Color.FromArgb(229, 231, 235), EnableHeadersVisualStyles = false, ColumnHeadersHeight = 32,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing, Visible = false
            };
            typeof(Control).GetProperty("DoubleBuffered", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(gridBom, true, null);
            gridBom.ColumnHeadersDefaultCellStyle.BackColor = Accent; gridBom.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            gridBom.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9.5f);
            gridBom.DefaultCellStyle.SelectionBackColor = Color.FromArgb(219, 234, 254); gridBom.DefaultCellStyle.SelectionForeColor = Color.Black;
            gridBom.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Place", HeaderText = Tx.ColPlace, FillWeight = 9 });
            foreach (var c in new[] { Tuple.Create("Category", Tx.ColCategory, 13f), Tuple.Create("Designator", "Designator", 28f), Tuple.Create("Value", Tx.ColPartValue, 16f),
                                      Tuple.Create("Footprint", "Footprint", 19f), Tuple.Create("Qty", Tx.ColQty, 6f) })
                gridBom.Columns.Add(new DataGridViewTextBoxColumn { Name = c.Item1, HeaderText = c.Item2, FillWeight = c.Item3, ReadOnly = true });
            gridBom.Columns.Add(new DataGridViewTextBoxColumn { Name = "LCSC", HeaderText = "LCSC ✎", FillWeight = 13, MaxInputLength = 12 });
            gridBom.Columns["Qty"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            gridBom.CellFormatting += BomFormat;
            gridBom.CurrentCellDirtyStateChanged += (o, e) => { if (gridBom.IsCurrentCellDirty && gridBom.CurrentCell is DataGridViewCheckBoxCell) gridBom.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            gridBom.CellValueChanged += BomChanged;
            // A code taken from the schematic is changed in KiCad (symbol field "LCSC Part"), not here
            gridBom.CellBeginEdit += (o, e) =>
            {
                var s = e.RowIndex >= 0 ? gridBom.Rows[e.RowIndex].Tag as BomRow : null;
                if (s != null && s.FromSchematic && gridBom.Columns[e.ColumnIndex].Name == "LCSC") e.Cancel = true;
            };

            lBomEmpty = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.DimGray, Text = Tx.BomEmptyHint };

            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 50, Padding = new Padding(0, 8, 0, 4) };
            bImport = MakeButton(Tx.ImportCodes, false); bImport.Dock = DockStyle.Left; bImport.Width = 170; bImport.Enabled = false;
            bBuild = MakeButton(Tx.BuildFiles, true); bBuild.Dock = DockStyle.Right; bBuild.Width = 220; bBuild.Enabled = false;
            bBuild.BackColor = bBuild.FlatAppearance.BorderColor = Disabled;   // disabled (grey) until a project is selected
            // Board count: the xlsx cost estimate and stock check use it (remembered)
            var boardsPanel = new Panel { Dock = DockStyle.Right, Width = 150, Padding = new Padding(0, 6, 10, 0) };
            nBoards = new NumericUpDown { Minimum = 1, Maximum = 100000, Value = 5, Width = 66, Location = new Point(76, 6), TextAlign = HorizontalAlignment.Center };
            int savedBoards;
            if (int.TryParse(env.GetSetting(KiCadEnvironment.SettingBoardCount) ?? "", NumberStyles.Integer, S.Inv, out savedBoards) && savedBoards >= 1 && savedBoards <= 100000) nBoards.Value = savedBoards;
            nBoards.ValueChanged += (o, e) => { try { env.SetSetting(KiCadEnvironment.SettingBoardCount, ((int)nBoards.Value).ToString(S.Inv)); } catch (Exception) { } };
            boardsPanel.Controls.Add(new Label { Text = Tx.BoardCount, AutoSize = true, Location = new Point(4, 9), ForeColor = Color.DimGray });
            boardsPanel.Controls.Add(nBoards);
            lOutputFolder = new LinkLabel { Text = Tx.OpenFolder, Dock = DockStyle.Right, Width = 90, TextAlign = ContentAlignment.MiddleCenter, Visible = false };
            lBomStatus = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(10, 0, 0, 0) };
            bottom.Controls.Add(lBomStatus); bottom.Controls.Add(lOutputFolder); bottom.Controls.Add(boardsPanel); bottom.Controls.Add(bBuild); bottom.Controls.Add(bImport);
            bImport.Click += (o, e) => ImportCodes();
            bBuild.Click += (o, e) => BuildProduction();
            lOutputFolder.LinkClicked += (o, e) =>
            {
                if (selectedProject == null) return;
                Directory.CreateDirectory(Production.OutputDir(selectedProject));
                using (System.Diagnostics.Process.Start("explorer.exe", "\"" + Production.OutputDir(selectedProject) + "\"")) { }
            };

            page.Controls.Add(gridBom); page.Controls.Add(lBomEmpty); page.Controls.Add(bottom); page.Controls.Add(top);

            // The last selected project is remembered
            string last = env.GetSetting(KiCadEnvironment.SettingLastProject);
            if (last != null && File.Exists(last)) Shown += (o, e) => SelectProject(last, false, !restarted);
            return page;
        }

        void SelectProject(string path) { SelectProject(path, true, true); }

        // Selecting a project syncs the left panel with the project's own settings (layers, thickness, copper, stackup, mask, finish).
        // syncPanel is false when the window is rebuilt after a language switch: the user's choices on the left are kept.
        void SelectProject(string path, bool notify, bool syncPanel)
        {
            ProjectInfo p;
            try { p = ProjectUpdater.Analyze(path); }
            catch (Exception e) { ShowError(e); return; }
            selectedProject = p;
            // The previous project's list must not stay editable while the new one loads (or if it fails to load)
            bom = null; gridBom.Rows.Clear(); gridBom.Visible = false; lBomEmpty.Visible = true;
            try { env.SetSetting(KiCadEnvironment.SettingLastProject, p.ProPath); } catch (Exception) { }
            string note = null;
            var s = syncPanel ? ProjectUpdater.SelectionFromProject(Maker, p, Collect(), out note) : null;
            if (s != null)
            {
                loading = true; nMargin.Value = (decimal)Math.Min(0.15, Math.Max(0, s.Margin)); loading = false;
                Fill(s);
            }
            if (notify)
            {
                var g = Collect();
                ShowBanner(s == null ? "⚠  " + note : "ⓘ  " + Tx.SettingsTakenFromProject(Tx.LayersN(g.Layers) + " · " + S.Mm(g.Thickness) + " · " + S.Oz(g.Outer) +
                    (g.Layers > 2 ? " / " + S.Oz(g.Inner) : "") + " · " + g.Stackup + " · " + cMask.Text.Replace(Tx.RecommendedMark, "")) + (note != null ? "  " + Tx.ClickForMore : ""),
                    s == null ? WarnBack : InfoBack, s == null ? WarnText : InfoText, s != null ? note : null);
            }
            lProject.Text = p.Name + "  ·  " + Tx.LayersN(p.Layers);
            lProjectSub.Text = p.Dir;
            lOutputFolder.Visible = true; bRefresh.Enabled = true;
            lBomStatus.Text = "⏳  " + Tx.ReadingBom; lBomStatus.ForeColor = Color.DimGray;
            bImport.Enabled = false; UpdateBomStatus();
            System.Threading.Tasks.Task.Factory.StartNew(() => Production.Bom(env, p)).ContinueWith(g =>
            {
                if (IsDisposed || !IsHandleCreated) return;
                try
                {
                    BeginInvoke((Action)(() =>
                    {
                        if (selectedProject != p) return;   // another project was selected meanwhile
                        if (g.Exception != null) { lBomStatus.Text = "✕  " + g.Exception.GetBaseException().Message; lBomStatus.ForeColor = Color.FromArgb(185, 28, 28); return; }
                        bom = g.Result; FillBom();
                    }));
                }
                catch (Exception) { }
            });
        }

        void FillBom()
        {
            fillingBom = true;
            gridBom.Rows.Clear();
            foreach (var s in bom.OrderBy(x => x.Category).ThenBy(x => x.Refs))
            {
                int i = gridBom.Rows.Add(!s.Excluded, s.Category, s.Refs, string.IsNullOrWhiteSpace(s.Value) ? Tx.EmptyValue : s.Value, s.Package, s.Qty, s.Lcsc);
                gridBom.Rows[i].Tag = s;
                gridBom.Rows[i].Cells["Designator"].ToolTipText = s.Refs;
                gridBom.Rows[i].Cells["Footprint"].ToolTipText = s.Footprint;
                if (s.FromSchematic) gridBom.Rows[i].Cells["LCSC"].ToolTipText = Tx.CodeFromSchematic;
            }
            fillingBom = false;
            gridBom.Visible = true; lBomEmpty.Visible = false; bImport.Enabled = !busy;
            UpdateBomStatus();
        }

        void BomFormat(object o, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var s = gridBom.Rows[e.RowIndex].Tag as BomRow;
            if (s == null) return;
            if (s.Excluded) { e.CellStyle.ForeColor = Disabled; if (gridBom.Columns[e.ColumnIndex].Name == "LCSC") e.CellStyle.BackColor = Color.FromArgb(243, 244, 246); return; }
            // blue: code from the schematic (read-only here), green: code entered in the app, red: missing or invalid
            if (gridBom.Columns[e.ColumnIndex].Name == "LCSC")
                e.CellStyle.BackColor = s.FromSchematic ? Color.FromArgb(219, 234, 254) : Production.IsValidCode(s.Lcsc) ? Color.FromArgb(220, 252, 231) : Color.FromArgb(254, 226, 226);
        }

        void BomChanged(object o, DataGridViewCellEventArgs e)
        {
            if (fillingBom || e.RowIndex < 0) return;
            var row = gridBom.Rows[e.RowIndex]; var s = row.Tag as BomRow;
            if (s == null) return;
            if (gridBom.Columns[e.ColumnIndex].Name == "LCSC")
            {
                // the schematic's code stays (a change that did not come through the editor, e.g. a paste, is undone)
                if (s.FromSchematic) { fillingBom = true; row.Cells["LCSC"].Value = s.Lcsc; fillingBom = false; return; }
                string code = Convert.ToString(row.Cells["LCSC"].Value ?? "").Trim().ToUpperInvariant();
                s.Lcsc = code;
                if (code != Convert.ToString(row.Cells["LCSC"].Value)) { fillingBom = true; row.Cells["LCSC"].Value = code; fillingBom = false; }
            }
            else if (gridBom.Columns[e.ColumnIndex].Name == "Place") s.Excluded = !(row.Cells["Place"].Value is bool && (bool)row.Cells["Place"].Value);
            else return;
            try { Production.SaveCodes(selectedProject, bom); } catch (Exception ex) { ShowError(ex); }
            gridBom.InvalidateRow(e.RowIndex);
            UpdateBomStatus();
        }

        void UpdateBomStatus()
        {
            if (bom == null)
            {
                bBuild.Enabled = false;
                bBuild.BackColor = bBuild.FlatAppearance.BorderColor = Disabled;
                return;
            }
            int missing = bom.Count(x => !x.Excluded && !Production.IsValidCode(x.Lcsc)), excluded = bom.Count(x => x.Excluded);
            string suffix = excluded > 0 ? "  ·  " + Tx.RowsNotPlaced(excluded) : "";
            int fromSchematic = bom.Count(x => !x.Excluded && x.FromSchematic);
            if (fromSchematic > 0) suffix = "  ·  " + Tx.RowsFromSchematic(fromSchematic) + suffix;
            lBomStatus.Text = missing == 0 ? "✓  " + Tx.AllCodesComplete(bom.Count(x => !x.Excluded)) + suffix : "✕  " + Tx.CodesMissingShort(missing) + suffix;
            lBomStatus.ForeColor = missing == 0 ? Color.FromArgb(21, 128, 61) : Color.FromArgb(185, 28, 28);
            bBuild.Enabled = missing == 0 && bom.Count > 0 && !busy;
            // Flat buttons do not change colour when disabled; turned grey so it is visibly unavailable
            bBuild.BackColor = bBuild.FlatAppearance.BorderColor = bBuild.Enabled ? Accent : Disabled;
        }

        void ImportCodes()
        {
            if (bom == null) return;
            string file;
            using (var d = new OpenFileDialog { Title = Tx.ImportCodesTitle, Filter = Tx.ImportCodesFilter, RestoreDirectory = true })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                file = d.FileName;
            }
            var conflicts = new List<string>();
            int filled;
            try { filled = Production.ImportCodes(file, bom, conflicts); Production.SaveCodes(selectedProject, bom); }
            catch (Exception e) { ShowError(e); return; }
            FillBom();
            ShowBanner((filled > 0 ? "✓  " : "⚠  ") + Tx.ImportedFrom(Path.GetFileName(file), filled) + (conflicts.Count > 0 ? "  " + Tx.ConflictsFound(conflicts.Count) : ""),
                filled > 0 ? OkBack : WarnBack, filled > 0 ? OkText : WarnText,
                conflicts.Count > 0 ? Tx.ConflictsDetail + "\n•  " + string.Join("\n•  ", conflicts) : null);
        }

        // Disables everything that could change the BOM or start another job while a background job runs
        void SetBusy(bool value)
        {
            busy = value;
            bApply.Enabled = !value && lastRules != null && buttonTimer == null;
            bProject.Enabled = !value;
            bImport.Enabled = !value && bom != null;
            // Only "Place" and "LCSC" are editable; the grid-wide flag also resets column flags, so they are set explicitly
            gridBom.ReadOnly = value;
            foreach (DataGridViewColumn c in gridBom.Columns) c.ReadOnly = value || (c.Name != "Place" && c.Name != "LCSC");
            UpdateBomStatus();
        }

        void BuildProduction()
        {
            if (selectedProject == null || bom == null || lastRules == null) return;
            if (!Dialogs.Confirm(this, Tx.BuildConfirm(Production.OutputDir(selectedProject), (int)nBoards.Value), Tx.ProductionFiles)) return;
            var selection = Collect(); var rules = lastRules; var m = Maker; var p = selectedProject; var list = bom;
            int boards = (int)nBoards.Value; var notes = new List<string>();
            SetBusy(true);
            string oldText = bBuild.Text; bBuild.Text = Tx.Building;
            ShowBanner("⏳  " + Tx.BuildingFiles, InfoBack, InfoText, null);
            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                var current = ProjectUpdater.Analyze(p.ProPath);   // the layer count may have changed meanwhile
                return Production.Build(env, current, m, selection, rules, list, boards, notes);
            }).ContinueWith(g =>
            {
                if (IsDisposed || !IsHandleCreated) return;
                try
                {
                    BeginInvoke((Action)(() =>
                    {
                        bBuild.Text = oldText; SetBusy(false);
                        if (g.Exception != null)
                            ShowBanner("✕  " + Tx.BuildFailed, ErrorBack, ErrorText, g.Exception.GetBaseException().Message);
                        else if (notes.Count > 0)
                            ShowBanner("⚠  " + Tx.BuildDoneWithWarnings(notes.Count), WarnBack, WarnText,
                                Tx.WarningsHeader + "\n•  " + string.Join("\n•  ", notes) + "\n\n" + Tx.CreatedFilesHeader + "\n•  " + string.Join("\n•  ", g.Result));
                        else
                            ShowBanner("✓  " + Tx.BuildDone(string.Join(", ", g.Result.Select(Path.GetFileName))), OkBack, OkText,
                                Tx.CreatedFilesHeader + "\n•  " + string.Join("\n•  ", g.Result));
                    }));
                }
                catch (Exception) { }
            });
        }
    }
}
