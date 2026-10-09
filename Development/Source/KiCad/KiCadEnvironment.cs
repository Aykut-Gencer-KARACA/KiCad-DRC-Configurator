using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace KiCadDrc
{
    // ------------------------------------------------------------------ KiCad environment

    class KiCadNotFoundException : Exception
    {
        public KiCadNotFoundException() : base(Tx.KiCadNotFound) { }
    }

    class KiCadEnvironment
    {
        public const string TemplateVariable = "KICAD_USER_TEMPLATE_DIR";
        public const string SettingKiCadFolder = "kicad_folder", SettingLastProject = "last_project", SettingBoardCount = "board_count", SettingLanguage = "language";
        public string Version, InstallDir, StockTemplate, DataDir, TemplateDir, Backup, StatePath, SettingsPath;
        public List<string> Warnings = new List<string>();

        // KiCad is searched in this order: manually chosen and saved folder -> Windows installed programs -> Program Files.
        // If none is found KiCadNotFoundException is thrown; the window asks the user for the folder (ChooseManually).
        public KiCadEnvironment() : this(null) { }

        KiCadEnvironment(string folder)
        {
            DataDir = DefaultDataDir();
            SettingsPath = Path.Combine(DataDir, "settings.json");
            MigrateSettings();
            folder = folder ?? SavedFolder() ?? FindInRegistry() ?? ScanProgramFiles();
            if (folder == null) throw new KiCadNotFoundException();
            Setup(folder);
        }

        public static string DefaultDataDir()
        {
            string exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string dir = Path.Combine(exeDir, DiskNames.DataFolder);
            MigrateDataFolder(exeDir, dir);
            return dir;
        }

        // Versions before 2.2 used Turkish folder names; the old data folder and its sub folders are renamed once
        static void MigrateDataFolder(string exeDir, string dir)
        {
            try
            {
                string old = Path.Combine(exeDir, DiskNames.LegacyDataFolder);
                if (Directory.Exists(old) && !Directory.Exists(dir)) Directory.Move(old, dir);
                if (!Directory.Exists(dir)) return;
                foreach (var pair in new[] {
                    Tuple.Create(DiskNames.LegacyProjectBackups, DiskNames.ProjectBackups),
                    Tuple.Create("backup", DiskNames.BackupFolder), Tuple.Create("templates", DiskNames.TemplatesFolder) })
                {
                    // Actual name on disk (Windows paths ignore case, so the listing is needed to see the stored case)
                    string from = Directory.GetDirectories(dir).FirstOrDefault(d => string.Equals(Path.GetFileName(d), pair.Item1, StringComparison.OrdinalIgnoreCase));
                    string to = Path.Combine(dir, pair.Item2);
                    if (from == null || Path.GetFileName(from) == pair.Item2) continue;
                    // Same name in another case (backup -> Backup): Windows needs a detour through a temporary name
                    if (string.Equals(pair.Item1, pair.Item2, StringComparison.OrdinalIgnoreCase))
                    {
                        string temp = to + ".renaming";
                        Directory.Move(from, temp);
                        Directory.Move(temp, to);
                    }
                    else if (!Directory.Exists(to)) Directory.Move(from, to);
                }
            }
            catch (Exception) { }   // folder in use: the old names keep working until the next start
        }

        // Valid KiCad folder: the "New Project" template and kicad.exe must exist
        public static bool IsValid(string folder)
        {
            return !string.IsNullOrEmpty(folder)
                && File.Exists(Path.Combine(folder, @"share\kicad\template\kicad.kicad_pro"))
                && File.Exists(Path.Combine(folder, @"bin\kicad.exe"));
        }

        static Version ReadVersion(string folder)
        {
            var v = System.Diagnostics.FileVersionInfo.GetVersionInfo(Path.Combine(folder, @"bin\kicad.exe"));
            return new Version(v.FileMajorPart, v.FileMinorPart);
        }

        string SavedFolder()
        {
            var k = GetSetting(SettingKiCadFolder);
            return IsValid(k) ? k : null;
        }

        static string FindInRegistry()
        {
            var candidates = new List<string>();
            var keys = new[]
            {
                Tuple.Create(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Uninstall"),
                Tuple.Create(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Uninstall"),
                Tuple.Create(Registry.LocalMachine, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
            };
            foreach (var a in keys)
                using (var root = a.Item1.OpenSubKey(a.Item2))
                {
                    if (root == null) continue;
                    foreach (var name in root.GetSubKeyNames())
                        using (var k = root.OpenSubKey(name))
                        {
                            var display = k == null ? null : k.GetValue("DisplayName") as string;
                            var location = k == null ? null : k.GetValue("InstallLocation") as string;
                            if (display != null && location != null && display.StartsWith("KiCad ")) candidates.Add(location.Trim('"').TrimEnd('\\'));
                        }
                }
            return Newest(candidates);
        }

        static string ScanProgramFiles()
        {
            var candidates = new List<string>();
            foreach (var root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) })
            {
                var kicad = Path.Combine(root, "KiCad");
                if (!Directory.Exists(kicad)) continue;
                candidates.Add(kicad);
                candidates.AddRange(Directory.GetDirectories(kicad));
            }
            return Newest(candidates);
        }

        // Newest version if several KiCad installations exist
        static string Newest(IEnumerable<string> candidates)
        {
            return candidates.Where(IsValid).OrderByDescending(ReadVersion).FirstOrDefault();
        }

        // Validates, uses and remembers the folder chosen by the user
        public static KiCadEnvironment ChooseManually(string folder)
        {
            if (!IsValid(folder)) throw new InvalidOperationException(Tx.KiCadNotInFolder(folder));
            var env = new KiCadEnvironment(folder);
            env.SetSetting(SettingKiCadFolder, folder);
            return env;
        }

        // ---- settings.json: reads/writes one key and keeps the others

        public string GetSetting(string name) { return ReadSettingFile(SettingsPath, name); }

        public static string ReadSettingStatic(string dataDir, string name) { return ReadSettingFile(Path.Combine(dataDir, "settings.json"), name); }

        static string ReadSettingFile(string path, string name)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var d = S.Json().Deserialize<Dictionary<string, object>>(File.ReadAllText(path, Encoding.UTF8));
                return d != null && d.ContainsKey(name) ? d[name] as string : null;
            }
            catch (Exception) { return null; }   // corrupt settings file: behave as if empty
        }

        public void SetSetting(string name, string value)
        {
            Dictionary<string, object> d = null;
            try { if (File.Exists(SettingsPath)) d = S.Json().Deserialize<Dictionary<string, object>>(File.ReadAllText(SettingsPath, Encoding.UTF8)); }
            catch (Exception) { }
            if (d == null) d = new Dictionary<string, object>();
            d[name] = value;
            S.Write(SettingsPath, JsonWriter.Write(d));
        }

        // Versions before 2.2 wrote settings/state files with Turkish key names; they are converted once
        void MigrateSettings()
        {
            try
            {
                string old = Path.Combine(DataDir, DiskNames.LegacySettingsFile);
                if (File.Exists(old) && !File.Exists(SettingsPath))
                {
                    var d = S.Json().Deserialize<Dictionary<string, object>>(File.ReadAllText(old, Encoding.UTF8)) ?? new Dictionary<string, object>();
                    var n = new Dictionary<string, object>();
                    foreach (var e in d) n[Legacy.SettingKey(e.Key)] = e.Value;
                    S.Write(SettingsPath, JsonWriter.Write(n));
                }
                if (File.Exists(old)) File.Delete(old);
                // The part cache is rebuilt under its new name
                string oldCache = Path.Combine(DataDir, DiskNames.LegacyPartCacheFile);
                if (File.Exists(oldCache)) File.Delete(oldCache);
            }
            catch (Exception) { }   // unreadable old file: settings start empty
        }

        void MigrateState()
        {
            try
            {
                string old = Path.Combine(Path.GetDirectoryName(StatePath), DiskNames.LegacyStateFile);
                if (!File.Exists(old)) return;
                if (!File.Exists(StatePath))
                {
                    var d = S.Json().Deserialize<Dictionary<string, object>>(File.ReadAllText(old, Encoding.UTF8)) ?? new Dictionary<string, object>();
                    var n = new OrderedMap();
                    foreach (var e in d)
                    {
                        string key = Legacy.StateKey(e.Key);
                        object value = e.Value;
                        if (key == "mask" && value is string) value = Legacy.MaskCode((string)value);
                        if (key == "edge" && value is string) value = Legacy.EdgeCode((string)value);
                        n.Put(key, value);
                    }
                    S.Write(StatePath, JsonWriter.Write(n));
                }
                File.Delete(old);
            }
            catch (Exception) { }
        }

        void Setup(string folder)
        {
            var v = ReadVersion(folder);
            Version = v.Major + "." + v.Minor; InstallDir = folder;
            StockTemplate = Path.Combine(folder, @"share\kicad\template\kicad.kicad_pro");
            TemplateDir = Path.Combine(DataDir, DiskNames.TemplatesFolder);
            Backup = Path.Combine(DataDir, DiskNames.BackupFolder, Version, "kicad.kicad_pro.orig");
            StatePath = Path.Combine(DataDir, DiskNames.BackupFolder, Version, "state.json");
            MigrateState();

            // If the folder was moved, KiCad's template path points to the old place: redirect it when a setting is applied
            if (File.Exists(StatePath) && Directory.Exists(TemplateDir) &&
                !string.Equals(Environment.GetEnvironmentVariable(TemplateVariable, EnvironmentVariableTarget.User), TemplateDir, StringComparison.OrdinalIgnoreCase))
                Environment.SetEnvironmentVariable(TemplateVariable, TemplateDir, EnvironmentVariableTarget.User);

            RefreshWarnings();
        }

        // The data folder is prepared at startup (even if it was deleted); problems are collected to show to the user
        public void RefreshWarnings()
        {
            try { Warnings = Prepare(); }
            catch (Exception e) { Warnings = new List<string> { Tx.DataFolderNotReady(e.Message) }; }
        }

        // Byte-identical copy of the untouched "New Project" template shipped with KiCad 10.0. If KiCad's file is already
        // modified and the backup is lost (data folder deleted), Restore defaults writes this back; KiCad fills in missing
        // fields with its own defaults, so it is also a valid starting file for other versions.
        const string OriginalTemplate = "{\r\n  \"board\": {\r\n    \"design_settings\": {\r\n      \"defaults\": {},\r\n      \"diff_pair_dimensions\": [],\r\n      \"drc_exclusions\": [],\r\n      \"rules\": {},\r\n      \"track_widths\": [],\r\n      \"via_dimensions\": []\r\n    }\r\n  },\r\n  \"boards\": [],\r\n  \"libraries\": {\r\n    \"pinned_footprint_libs\": [],\r\n    \"pinned_symbol_libs\": []\r\n  },\r\n  \"meta\": {\r\n    \"filename\": \"kicad.kicad_pro\",\r\n    \"version\": 1\r\n  },\r\n  \"net_settings\": {\r\n    \"classes\": [],\r\n    \"meta\": {\r\n      \"version\": 0\r\n    }\r\n  },\r\n  \"pcbnew\": {\r\n    \"page_layout_descr_file\": \"\"\r\n  },\r\n  \"sheets\": [],\r\n  \"text_variables\": {}\r\n}\r\n";

        // Creates the folders, takes the backup and finds inconsistencies. Runs at startup and before every operation.
        List<string> Prepare()
        {
            var warnings = new List<string>();
            // The whole data folder layout is created up front (also after the user deletes it)
            Directory.CreateDirectory(TemplateDir);
            Directory.CreateDirectory(Path.GetDirectoryName(Backup));
            Directory.CreateDirectory(Path.Combine(DataDir, DiskNames.ProjectBackups));
            bool stockChanged = StockChanged();
            if (!File.Exists(Backup))
            {
                if (stockChanged) S.Write(Backup, OriginalTemplate);   // KiCad's file is not original: back up the embedded original
                else File.Copy(StockTemplate, Backup);
            }

            var state = State();
            if (StateCorrupt) warnings.Add(Tx.WarnStateCorrupt);
            if (state != null)
            {
                string name = state.ContainsKey("template") ? state["template"] as string : null;
                if (name != null && !Directory.Exists(Path.Combine(TemplateDir, name))) warnings.Add(Tx.WarnTemplateDeleted(name));
                if (stockChanged) warnings.Add(Tx.WarnStockChangedByOld);
            }
            else
            {
                string env = Environment.GetEnvironmentVariable(TemplateVariable, EnvironmentVariableTarget.User);
                if (stockChanged && !StateCorrupt) warnings.Add(Tx.WarnStockChangedNoRecord);
                else if (!StateCorrupt && env != null && IsOurTemplatePath(env)) warnings.Add(Tx.WarnStaleTemplatePath);
            }
            return warnings;
        }

        // Writes KiCad's "New Project" file. If KiCad is installed in an administrator-owned folder a normal user cannot
        // write there; then the app re-runs itself as administrator (UAC prompt) for this single copy only.
        public const string ElevatedCopyArg = "--admin-copy", PendingFile = "kicad.kicad_pro.pending";

        void WriteStock(byte[] data)
        {
            try { File.WriteAllBytes(StockTemplate, data); return; }
            catch (UnauthorizedAccessException) { }

            string temp = Path.Combine(DataDir, PendingFile);
            File.WriteAllBytes(temp, data);
            try
            {
                var info = new System.Diagnostics.ProcessStartInfo(Assembly.GetExecutingAssembly().Location,
                    ElevatedCopyArg + " \"" + temp + "\" \"" + StockTemplate + "\"")
                { UseShellExecute = true, Verb = "runas", WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden };
                System.Diagnostics.Process p;
                try { p = System.Diagnostics.Process.Start(info); }
                catch (System.ComponentModel.Win32Exception e)
                {
                    if (e.NativeErrorCode == 1223) throw new InvalidOperationException(Tx.AdminDenied(InstallDir));   // user pressed "No" in UAC
                    throw;
                }
                using (p)
                {
                    p.WaitForExit();
                    if (p.ExitCode != 0) throw new InvalidOperationException(Tx.AdminCopyFailed(p.ExitCode, StockTemplate));
                }
            }
            finally { try { File.Delete(temp); } catch (IOException) { } }
        }

        // Elevated copy: only copies the file prepared by the app over KiCad's "New Project" file.
        // Both paths are validated so that nothing else can be written.
        public static int ElevatedCopy(string source, string target)
        {
            try
            {
                var t = new FileInfo(target); var s = new FileInfo(source);
                bool targetValid = t.Name == "kicad.kicad_pro" && t.Exists && t.Directory.Name == "template"
                    && t.Directory.Parent != null && t.Directory.Parent.Name == "kicad"
                    && t.Directory.Parent.Parent != null && t.Directory.Parent.Parent.Name == "share"
                    && IsValid(t.Directory.Parent.Parent.Parent.FullName);
                bool sourceValid = s.Name == PendingFile && s.Exists && s.Length < 1000000;
                if (!targetValid || !sourceValid) return 3;
                if (t.IsReadOnly) t.IsReadOnly = false;
                File.Copy(s.FullName, t.FullName, true);
                return 0;
            }
            catch (Exception) { return 2; }
        }

        // Does KiCad's "New Project" file contain rules (the original has an empty "rules": {}). An unreadable/corrupt file
        // also counts as "changed", so the backup comes from the embedded original and Restore defaults fixes the file.
        bool StockChanged()
        {
            IDictionary<string, object> stock;
            try { stock = S.Json().DeserializeObject(File.ReadAllText(StockTemplate)) as IDictionary<string, object>; }
            catch (ArgumentException) { return true; }   // invalid JSON
            if (stock == null) return true;
            var board = stock.ContainsKey("board") ? stock["board"] as IDictionary<string, object> : null;
            var ds = board != null && board.ContainsKey("design_settings") ? board["design_settings"] as IDictionary<string, object> : null;
            var rules = ds != null && ds.ContainsKey("rules") ? ds["rules"] as IDictionary<string, object> : null;
            return rules != null && rules.Count > 0;
        }

        // Does KiCad's template variable point to this app's folder (this copy, or an old/deleted copy)
        bool IsOurTemplatePath(string path)
        {
            return string.Equals(path, TemplateDir, StringComparison.OrdinalIgnoreCase)
                || path.EndsWith("\\" + DiskNames.DataFolder + "\\" + DiskNames.TemplatesFolder, StringComparison.OrdinalIgnoreCase)
                || path.EndsWith("\\" + DiskNames.LegacyDataFolder + "\\templates", StringComparison.OrdinalIgnoreCase)
                || !Directory.Exists(path);
        }

        // A corrupt (hand-edited, half-written) state file must not lock the app: it is ignored, reported at startup,
        // and overwritten or deleted by the next Apply / Restore defaults
        public bool StateCorrupt;

        public IDictionary<string, object> State()
        {
            StateCorrupt = false;
            if (!File.Exists(StatePath)) return null;
            try
            {
                var d = S.Json().Deserialize<Dictionary<string, object>>(File.ReadAllText(StatePath, Encoding.UTF8));
                if (d != null) Selection.FromMap(d);   // field types must be valid too
                return d;
            }
            catch (Exception) { StateCorrupt = true; return null; }
        }

        void ClearTemplates()
        {
            if (!Directory.Exists(TemplateDir)) return;
            foreach (var d in Directory.GetDirectories(TemplateDir, "DRC_*")) Directory.Delete(d, true);
        }

        public string Apply(IManufacturer m, Selection s, RuleSet r)
        {
            if (new Version(Version).Major < 10) throw new InvalidOperationException(Tx.NeedsKiCad10(Version));
            Prepare();
            s.Stackup = r.Stackup.Name;
            var state = State();
            object previousDir = state != null && state.ContainsKey("previous_template_dir")
                ? state["previous_template_dir"]
                : Environment.GetEnvironmentVariable(TemplateVariable, EnvironmentVariableTarget.User);
            if (previousDir is string && IsOurTemplatePath((string)previousDir)) previousDir = null;

            // All content is generated first; writing starts with the KiCad folder so nothing changes if permission is missing
            string name = "DRC_" + m.Code + "_" + s.Layers + "L";
            var files = new Dictionary<string, string>
            {
                { name + ".kicad_pro", Files.Pro(r, name + ".kicad_pro") },
                { name + ".kicad_pcb", Files.Pcb(s, r) },
                { name + ".kicad_dru", Files.Dru(r) },
                { @"meta\info.html", Files.InfoHtml(m, s, r, name) },
            };
            // Plain "File › New Project" keeps KiCad's own rules: its file is not written. If an older version wrote it,
            // it is restored to the original (only then administrator rights may be needed).
            if (StockChanged()) WriteStock(File.ReadAllBytes(Backup));

            ClearTemplates();
            string folder = Path.Combine(TemplateDir, name);
            foreach (var f in files) S.Write(Path.Combine(folder, f.Key), f.Value);
            Environment.SetEnvironmentVariable(TemplateVariable, TemplateDir, EnvironmentVariableTarget.User);

            var record = s.ToMap().Put("template", name).Put("date", DateTime.Now.ToString("yyyy-MM-dd HH:mm")).Put("previous_template_dir", previousDir);
            S.Write(StatePath, JsonWriter.Write(record));
            Warnings.Clear();
            return name;
        }

        public void RestoreDefaults()
        {
            Prepare();   // if the backup was deleted it is recreated first (from the embedded original if needed)
            // Written only when really changed, so administrator rights are not requested needlessly
            if (StockChanged()) WriteStock(File.ReadAllBytes(Backup));
            ClearTemplates();
            var state = State();
            string previousDir = state != null && state.ContainsKey("previous_template_dir") ? state["previous_template_dir"] as string : null;
            if (previousDir != null && IsOurTemplatePath(previousDir)) previousDir = null;
            // Undo the variable if it points to this app's folder (or a moved/deleted copy of it)
            string env = Environment.GetEnvironmentVariable(TemplateVariable, EnvironmentVariableTarget.User);
            if (env != null && IsOurTemplatePath(env))
                Environment.SetEnvironmentVariable(TemplateVariable, previousDir, EnvironmentVariableTarget.User);
            if (File.Exists(StatePath)) File.Delete(StatePath);
            Warnings.Clear();
        }
    }
}
