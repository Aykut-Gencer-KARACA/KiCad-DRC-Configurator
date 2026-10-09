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
    // ------------------------------------------------------------------ dialogs
    // Every message box of the window goes through here, so an automated test can answer them instead of a person.

    static class Dialogs
    {
        public static Func<IWin32Window, string, string, bool> Confirm = (owner, text, title) =>
            MessageBox.Show(owner, text, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

        public static Action<IWin32Window, string, string, MessageBoxIcon> Show = (owner, text, title, icon) =>
            MessageBox.Show(owner, text, title, MessageBoxButtons.OK, icon);
    }
}
