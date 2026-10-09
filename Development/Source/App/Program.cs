// KiCad DRC Configurator
// Writes manufacturer rules into a KiCad project template (stackup + board rules + custom rules), applies them to
// existing projects and builds manufacturing packages (gerber, BOM, CPL, XLSX). Build: build.ps1 (.NET Framework 4 csc, C# 5).
//
// What is touched in KiCad:
//   <KiCad>\share\kicad\template\kicad.kicad_pro   plain "New Project" copies this; it is NOT written any more (KiCad keeps its
//                                                    own rules); if an older version changed it, it is restored to the original
//   HKCU environment variable KICAD_USER_TEMPLATE_DIR  "User Templates" tab points to <exe folder>\<data folder>\templates
// KiCad location is not fixed: saved choice -> Windows installed programs -> Program Files -> ask the user.
// The app is portable: everything else lives in the data folder next to the exe (AppData and Temp are not used).
// All user-facing text is in Localization\Tx.*.cs (English / Turkish).

using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

[assembly: AssemblyTitle("KiCad DRC Configurator")]
[assembly: AssemblyProduct("KiCad DRC Configurator")]
[assembly: AssemblyVersion("2.2.0.0")]
[assembly: AssemblyCompany("Aykut Gencer Karaca")]
[assembly: AssemblyCopyright("© 2026 Aykut Gencer Karaca")]

namespace KiCadDrc
{
    static class Program
    {
        [DllImport("user32.dll")] internal static extern bool SetProcessDPIAware();

        [STAThread]
        static int Main(string[] args)
        {
            // Invariant culture everywhere, background tasks included (thread pool threads would otherwise use the Windows
            // culture: Turkish writes 0,5 and changes i/I casing)
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            // Single-file copy started with administrator rights (KiCad folder is write protected); does nothing else
            if (args.Length == 3 && args[0] == KiCadEnvironment.ElevatedCopyArg) return KiCadEnvironment.ElevatedCopy(args[1], args[2]);
            bool cli = args.Length > 0;
            if (cli)
            {
                // Console.OutputEncoding has no effect in a windowed exe; write UTF-8 directly so Turkish letters survive
                Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true });
                Console.SetError(new StreamWriter(Console.OpenStandardError(), new UTF8Encoding(false)) { AutoFlush = true });
            }
            else
            {
                SetProcessDPIAware();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
            }
            // Language: --lang en|tr, otherwise the saved setting, otherwise the Windows display language
            Tx.Turkish = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "tr";
            string savedLanguage = KiCadEnvironment.ReadSettingStatic(KiCadEnvironment.DefaultDataDir(), KiCadEnvironment.SettingLanguage);
            if (savedLanguage == "en" || savedLanguage == "tr") Tx.Turkish = savedLanguage == "tr";
            int langArg = Array.IndexOf(args, "--lang");
            if (langArg >= 0 && langArg + 1 < args.Length) Tx.Turkish = args[langArg + 1] == "tr";
            try
            {
                KiCadEnvironment env;
                int kicadArg = Array.IndexOf(args, "--kicad");
                if (kicadArg >= 0 && kicadArg + 1 < args.Length) env = KiCadEnvironment.ChooseManually(args[kicadArg + 1]);
                else
                {
                    try { env = new KiCadEnvironment(); }
                    catch (KiCadNotFoundException) { if (cli) throw; env = AskForKiCad(); if (env == null) return 1; }
                }
                var manufacturers = new List<IManufacturer> { new Jlc() };
                if (cli) return Cli.Run(env, manufacturers, args);

                // The window always leaves a settings file with its language (the command line never saves --lang)
                if (env.GetSetting(KiCadEnvironment.SettingLanguage) == null)
                    try { env.SetSetting(KiCadEnvironment.SettingLanguage, Tx.Turkish ? "tr" : "en"); } catch (Exception) { }

                // Changing the language rebuilds the window with the same selection and tab
                Selection start = null; int tab = 0;
                while (true)
                {
                    var form = new MainForm(env, manufacturers, start);
                    form.SelectTab(tab);
                    Application.Run(form);
                    if (!form.RestartRequested) break;
                    start = form.CurrentSelection; tab = form.CurrentTab;
                    env.RefreshWarnings();
                }
                return 0;
            }
            catch (Exception e)
            {
                if (cli) { Console.Error.WriteLine("ERROR: " + e.Message); return 1; }
                MessageBox.Show(e.Message, "KiCad DRC", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }

        // KiCad could not be found automatically: ask for its folder; the choice is saved in the settings file
        static KiCadEnvironment AskForKiCad()
        {
            MessageBox.Show(Tx.KiCadNotFoundIntro, Tx.KiCadFolderTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
            while (true)
            {
                using (var d = new FolderBrowserDialog { Description = Tx.KiCadFolderPrompt, ShowNewFolderButton = false })
                {
                    if (d.ShowDialog() != DialogResult.OK) return null;
                    try { return KiCadEnvironment.ChooseManually(d.SelectedPath); }
                    catch (Exception e) { MessageBox.Show(e.Message, Tx.KiCadFolderTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
                }
            }
        }
    }
}
