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
        public void ShowBanner(string text, Color back, Color fore, string detail)
        {
            if (bannerTimer != null) { bannerTimer.Stop(); bannerTimer.Dispose(); bannerTimer = null; }
            lBanner.Text = text; banner.BackColor = back; banner.ForeColor = fore; lBanner.ForeColor = fore;
            bannerDetail = detail; lBanner.Cursor = detail != null ? Cursors.Hand : Cursors.Default;
            if (!banner.Visible)
            {
                float dpi; using (var g = CreateGraphics()) dpi = g.DpiX;
                int target = (int)(44 * dpi / 96f);
                banner.Height = 0; banner.Visible = true;
                Anim.Play(260, t => banner.Height = (int)(target * t), null);
            }
            // Success and info banners close by themselves; warning, error and waiting banners stay until closed
            if (!(text.StartsWith("⚠") || text.StartsWith("✕") || text.StartsWith("⏳"))) bannerTimer = Anim.After(9000, HideBanner);
        }

        void HideBanner()
        {
            if (bannerTimer != null) { bannerTimer.Stop(); bannerTimer.Dispose(); bannerTimer = null; }
            if (!banner.Visible) return;
            int h = banner.Height;
            Anim.Play(220, t => banner.Height = (int)(h * (1 - t)), () => banner.Visible = false);
        }

        // Flashes the given rows yellow and fades them back to their normal colour
        void FlashItems(List<ListViewItem> items)
        {
            if (items.Count == 0) return;
            var target = items.Select(o => o.BackColor == CopperColor ? CopperColor : Color.White).ToList();
            Anim.Play(1100, t =>
            {
                for (int i = 0; i < items.Count; i++)
                    if (items[i].ListView != null) items[i].BackColor = Anim.Blend(Flash, target[i], t);
            }, null);
        }

        void ShowError(Exception e)
        {
            // If the KiCad folder needs permission the app asks for administrator rights itself; permission errors that reach
            // here concern the app's own folder. Controlled Folder Access sometimes shows up as "path not found" on Desktop/Documents.
            string extra = e is UnauthorizedAccessException || e is DirectoryNotFoundException ? "\n\n" + Tx.DataFolderNotWritable : "";
            Dialogs.Show(this, e.Message + extra, Tx.Error, MessageBoxIcon.Error);
        }

        void Help()
        {
            Dialogs.Show(this, Tx.HelpText, Tx.HowToUse, MessageBoxIcon.Information);
        }
    }
}
