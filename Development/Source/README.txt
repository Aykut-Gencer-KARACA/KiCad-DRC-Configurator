KiCad DRC Configurator - development files

Folder layout
  KiCad DRC.exe                     the app (the only thing a user needs)
  Data\                             app data; created by the app on first start
      Backup\<KiCad version>\       KiCad's original "New Project" file + state.json (do not delete)
      Templates\                    generated project template (KiCad "User Templates" points here)
      Project Backups\              project backups taken before "Apply to existing project"
      settings.json                 language, KiCad folder if chosen manually, last project, board count
      part-cache.json               part information from JLC (manufacturer, MPN, stock, price); valid for 3 days
  Development\
      Source\                       this folder
      Reference Data\               JLCPCB pages and the stackup database (SOURCES.txt)
      Test Results\                 verification reports, project update test, sample production files

Production files (written into <KiCad project>\JLC_Production\):
  <project>_Rev<rev>_Gerber.zip     gerber + drill package for the JLC order page (JLC's KiCad 9 settings: zone fills
                                    checked, X2, PTH and NPTH drill files separate)
  <project>_Rev<rev>_BOM_JLC.csv    BOM (columns as the Fabrication Toolkit plugin, with the LCSC codes)
  <project>_Rev<rev>_CPL_JLC.csv    placement file (columns as the plugin; only the placed BOM parts; bottom rotation 180 - angle;
                                    plugin rotation corrections for KiCad library footprints, none for EasyEDA ones)
  <project>_Rev<rev>_Netlist.ipc    IPC-D-356 netlist
  <project>_Rev<rev>_Production.xlsx summary, BOM with prices/stock, CPL (in the selected language)
  The revision comes from the PCB title block, otherwise from the folder name (e.g. "RevC"); without one "_Rev<rev>" is left out.

Source code (C# 5, .NET Framework 4)  - every .cs file below this folder is compiled into the exe
  App\                  Program.cs (entry point, language, window restart), Cli.cs (command line)
  Common\               Helpers.cs (S, Proc, JsonWriter, OrderedMap), Model.cs (Selection, Stackup, RuleSet, IManufacturer),
                        DiskNames.cs (folder/file names), Legacy.cs (names used before 2.2, for migration)
  Localization\         Tx.*.cs: ALL user-facing text in English and Turkish, one file per area (one partial class Tx)
  Manufacturers\        Jlc.cs: JLCPCB rules (IManufacturer implementation)
  KiCad\                KiCadEnvironment.cs (location, template, settings, state), KiCadFiles.cs (file generation),
                        Sexp.cs (S-expressions), ProjectUpdater.cs (apply to an existing project), Verifier.cs (KiCad check)
  Production\           Models.cs, JlcParts.cs (part information), Production.cs (BOM, CPL, gerber), Xlsx.cs
  Calculators\          PCB calculators: Calculator.cs (base class + list), CalcUi.cs (shared view parts), Materials.cs,
                        one folder per calculator: <Name>Math.cs (formulas, unit tested) + <Name>Calculator.cs (view)
  UI\                   MainForm.*.cs (window, one file per area), Dialogs.cs (message boxes, replaceable in tests), Anim.cs

  build.ps1             builds -> ..\..\KiCad DRC.exe
                        powershell -ExecutionPolicy Bypass -File build.ps1
  Tests\run-tests.ps1   runs the KiCad verification in 26 scenarios, writes a report to Test Results (applies nothing to KiCad)
                        powershell -ExecutionPolicy Bypass -File Tests\run-tests.ps1 [-Lang en|tr]
  Tests\unit-tests.ps1  unit tests of pure functions (file names, prices, CSV, S-expressions, .kicad_dru, JSON, plurals, calculators)
                        powershell -ExecutionPolicy Bypass -File Tests\unit-tests.ps1 -Exe "..\..\KiCad DRC.exe"
  Tests\gui-tests.ps1   drives the real window off screen and checks the user flows (language switch, production tab,
                        apply to project, apply + verify, restore defaults, calculators) on a COPY of a project
                        powershell -ExecutionPolicy Bypass -File Tests\gui-tests.ps1 -Exe "<copy>\KiCad DRC.exe" -Project X.kicad_pro [-Codes parts.md]
                        use an exe copy in its own folder (it creates its own Data folder); KiCad's template variable is restored
  Tests\saturn-compare-*.ps1   parallel check of a calculator against Saturn PCB Toolkit (must be installed; not part of
                        this project, https://www.saturnpcb.com). Reports: Test Results\saturn-comparison_NN_<name>.txt
  Tools\saturn-driver.ps1      remote control of Saturn's window (find controls, type values, read results, switch tabs)
  Tools\update-stackups.js     refreshes Reference Data\stackups.json from JLC's API:  node Tools\update-stackups.js
  Tools\compare-project.js     "Apply to existing project" test: .kicad_pro differences and that drawings stayed the same

PCB calculators: the formulas come from published sources (IPC standards, handbooks). Saturn PCB Toolkit is only used as a
black-box reference: the same inputs are given to both and the results compared. Deliberate differences are documented in the
comparison report and in the calculator's *Math.cs file.
Command line (for tests):
  "KiCad DRC.exe" --help
  "KiCad DRC.exe" --lang en --layers 6 --verify [--keep]                  verify with KiCad (--keep keeps the test board)
  "KiCad DRC.exe" --layers 6 --project X.kicad_pro --analyze               inspect a project (layers, inner layer usage, blockers)
  "KiCad DRC.exe" --layers 6 --project X.kicad_pro --update                apply to the project (backup: Data\Project Backups)
  "KiCad DRC.exe" --layers 6 --project X.kicad_pro --bom [--import-codes parts.md] [--build --boards 10]
                                                                           list the BOM / build the production files
  The exe is a GUI app: in PowerShell add "| Out-String" so the call waits for the output.
  KICAD_DRC_OFFLINE=1 environment variable: no connection to JLC (to test the xlsx without internet).

Language: EN / TR at the top right of the window (saved in settings.json); on the command line --lang en|tr.
New text: add a property to Tx in the matching Localization\Tx.*.cs file with both languages, never put text directly into the other files.

New manufacturer: write a class implementing IManufacturer (see Jlc.cs), add it to the list in Program.Main,
build with build.ps1 and test with run-tests.ps1.

KiCad location: manually chosen folder -> Windows installed programs -> Program Files -> ask the user.
To pass it by hand for a test:  "KiCad DRC.exe" --kicad "C:\KiCad folder" --show
If you move the folder, open the app once; it points KiCad's template path to the new place.
Do not put it on Desktop/Documents: Windows Controlled Folder Access blocks writing there.

Versions before 2.2 used Turkish names (data folder "Uygulama Dosyaları", ayarlar.json, durum.json, .lcsc.json keys,
.kicad_dru block markers). They are read, converted and renamed automatically at startup.
