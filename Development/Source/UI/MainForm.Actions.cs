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
        void Apply()
        {
            if (lastRules == null || busy) return;
            if (buttonTimer != null) { buttonTimer.Stop(); buttonTimer.Dispose(); buttonTimer = null; }
            bApply.Text = Tx.Applying; bApply.Enabled = false; bApply.Update();
            Cursor = Cursors.WaitCursor;
            string name;
            var selection = Collect(); var rules = lastRules;
            try { name = env.Apply(Maker, selection, rules); WriteStatus(); }
            catch (Exception e)
            {
                Cursor = Cursors.Default; bApply.Enabled = true; bApply.Text = Tx.Apply;
                ShowError(e); return;
            }
            Cursor = Cursors.Default;

            // The written settings are verified with KiCad's own tool in the background; meanwhile the button shows dots
            int dots = 0; SetBusy(true);
            var walker = new System.Windows.Forms.Timer { Interval = 350 };
            walker.Tick += (o, e) => bApply.Text = Tx.Verifying + new string('.', ++dots % 4);
            walker.Start(); bApply.Text = Tx.Verifying;
            ShowBanner("⏳  " + Tx.AppliedVerifying, InfoBack, InfoText, null);

            System.Threading.Tasks.Task.Factory.StartNew(() => Verifier.Run(env, selection, rules)).ContinueWith(g =>
            {
                if (IsDisposed || !IsHandleCreated) return;
                try { BeginInvoke((Action)(() => VerificationDone(g, walker, name))); }
                catch (Exception) { }   // the window was closed meanwhile (InvalidOperation / ObjectDisposed)
            });
        }

        void VerificationDone(System.Threading.Tasks.Task<Verification> g, System.Windows.Forms.Timer walker, string name)
        {
            walker.Stop(); walker.Dispose();
            var v = g.Exception != null ? new Verification { Error = g.Exception.GetBaseException().Message } : g.Result;
            Color target = v.Success ? Green : v.Error != null ? Color.FromArgb(217, 119, 6) : Color.FromArgb(220, 38, 38);
            bApply.Text = v.Success ? "✓  " + Tx.Applied : "⚠  " + Tx.CheckIt;
            Anim.Play(250, t => bApply.BackColor = bApply.FlatAppearance.BorderColor = Anim.Blend(Accent, target, t), null);
            buttonTimer = Anim.After(v.Success ? 2500 : 6000, () =>
            {
                buttonTimer = null; bApply.Text = Tx.Apply; bApply.Enabled = lastRules != null && !busy;
                Anim.Play(400, t => bApply.BackColor = bApply.FlatAppearance.BorderColor = Anim.Blend(target, lastRules != null ? Accent : Disabled, t), null);
            });
            SetBusy(false);
            bApply.Enabled = lastRules != null;
            if (v.Success)
                ShowBanner("✓  " + Tx.AppliedAndVerified(v.Passed.Count, name), OkBack, OkText, Tx.PassedChecks + "\n•  " + string.Join("\n•  ", v.Passed));
            else if (v.Error != null)
                ShowBanner("⚠  " + Tx.AppliedNotVerified, WarnBack, WarnText, v.Summary());
            else
                ShowBanner("✕  " + Tx.VerificationFailed(v.Failed.Count), ErrorBack, ErrorText, v.Summary());
        }

        // Applies the selected settings (layers, stackup, rules) to an existing KiCad project
        void ApplyToProject()
        {
            if (lastRules == null || busy) return;
            // If a project is selected on the production tab it is used (the confirmation still shows it)
            string path = selectedProject != null && File.Exists(selectedProject.ProPath) ? selectedProject.ProPath : null;
            if (path == null)
                using (var d = new OpenFileDialog { Title = Tx.SelectProjectToUpdate, Filter = Tx.KiCadProjectFilter, RestoreDirectory = true })
                {
                    if (d.ShowDialog(this) != DialogResult.OK) return;
                    path = d.FileName;
                }
            ProjectInfo p;
            try { p = ProjectUpdater.Analyze(path); }
            catch (Exception e) { ShowError(e); return; }
            var selection = Collect(); var rules = lastRules; var m = Maker;

            if (p.Locks.Count > 0)
            {
                Dialogs.Show(this, Tx.ProjectOpenWarning(p.Name), Tx.ProjectOpenTitle, MessageBoxIcon.Warning);
                return;
            }
            var plan = ProjectUpdater.Plan(p, selection.Layers);
            if (plan.Blocker != null) { Dialogs.Show(this, plan.Blocker, Tx.CannotReduceLayers, MessageBoxIcon.Warning); return; }

            string summary = Tx.ApplyToProjectConfirm(p.Name, p.Dir, plan, rules.Stackup.Name, S.Mm(Files.BoardThickness(rules)),
                m.Name + ", " + S.Oz(selection.Outer) + (selection.Layers > 2 ? " / " + S.Oz(selection.Inner) : ""), S.Mm(rules.Margin));
            if (!Dialogs.Confirm(this, summary, Tx.ApplyToProject)) return;

            SetBusy(true);
            int dots = 0; string oldText = bProject.Text;
            var walker = new System.Windows.Forms.Timer { Interval = 350 };
            walker.Tick += (o, e) => bProject.Text = Tx.UpdatingProject + new string('.', ++dots % 4);
            walker.Start(); bProject.Text = Tx.UpdatingProject;
            ShowBanner("⏳  " + Tx.UpdatingProjectBanner(p.Name), InfoBack, InfoText, null);

            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                string e1, e2;
                var before = Verifier.CountDrc(env, p.PcbPath, out e1);
                var result = ProjectUpdater.Update(env, m, selection, rules, p);
                var after = Verifier.CountDrc(env, p.PcbPath, out e2);
                return Tuple.Create(result, before, after, e1 ?? e2);
            }).ContinueWith(g =>
            {
                if (IsDisposed || !IsHandleCreated) return;
                try { BeginInvoke((Action)(() => ProjectDone(g, walker, oldText, p, plan))); }
                catch (Exception) { }   // the window was closed meanwhile
            });
        }

        void ProjectDone(System.Threading.Tasks.Task<Tuple<UpdateResult, int[], int[], string>> g, System.Windows.Forms.Timer walker,
            string oldText, ProjectInfo p, UpdatePlan plan)
        {
            walker.Stop(); walker.Dispose();
            bProject.Text = oldText; SetBusy(false);
            if (g.Exception != null)
            {
                ShowBanner("✕  " + Tx.ProjectNotUpdated(p.Name), ErrorBack, ErrorText, g.Exception.GetBaseException().Message);
                return;
            }
            SelectProject(p.ProPath, false, true);   // selected project, left panel and BOM are refreshed (layer count may have changed)
            var result = g.Result.Item1; int[] before = g.Result.Item2, after = g.Result.Item3;
            string layers = plan.From == plan.To ? Tx.LayersN(plan.To) : plan.From + " → " + Tx.LayersN(plan.To);
            string drc = Tx.DrcBeforeNow(Verifier.DrcText(before), Verifier.DrcText(after));
            string detail = Tx.ChangesHeader + "\n•  " + string.Join("\n•  ", result.Changes) + "\n\n" + drc +
                (g.Result.Item4 != null ? "\n(" + Tx.DrcNote + ": " + g.Result.Item4 + ")" : "") +
                "\n\n" + Tx.BackupHeader + "\n" + result.Backup + "\n\n" + Tx.OpenAndRunDrc;
            bool worse = before != null && after != null && after[0] > before[0];
            ShowBanner((worse ? "⚠  " : "✓  ") + Tx.ProjectUpdated(p.Name, layers) + "  " + drc +
                (worse ? "  — " + Tx.NewErrorsFound(after[0] - before[0]) : "  — " + Tx.ClickForDetails),
                worse ? WarnBack : OkBack, worse ? WarnText : OkText, detail);
        }

        void RestoreDefaults()
        {
            if (busy) return;
            if (!Dialogs.Confirm(this, Tx.RestoreConfirm, Tx.RestoreDefaults)) return;
            try
            {
                env.RestoreDefaults(); WriteStatus();
                ShowBanner("↺  " + Tx.Restored, Color.FromArgb(224, 231, 255), Color.FromArgb(49, 46, 129), null);
            }
            catch (Exception e) { ShowError(e); }
        }
    }
}
