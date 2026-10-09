# Drives the real window of the app without showing it (off screen) and checks the main user flows:
# language switch + restart, production tab (codes, "Place", build), apply to project, apply template + verification,
# restore defaults. Dialogs are answered by the script (Dialogs class). Works on a COPY of the given project.
#   powershell -ExecutionPolicy Bypass -File gui-tests.ps1 -Exe "test\KiCad DRC.exe" -Project "X.kicad_pro" [-Codes parts.md]
# Use an exe in a separate folder: it creates its own Data folder next to it. KiCad's template variable is restored at the end.
param([Parameter(Mandatory = $true)][string]$Exe, [Parameter(Mandatory = $true)][string]$Project, [string]$Codes, [string]$Work)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
[Threading.Thread]::CurrentThread.CurrentCulture = [Globalization.CultureInfo]::InvariantCulture
[Globalization.CultureInfo]::DefaultThreadCurrentCulture = [Globalization.CultureInfo]::InvariantCulture

$exe = (Resolve-Path $Exe).Path
if (-not $Work) { $Work = Join-Path (Split-Path $exe -Parent) 'gui-test-project' }
$envBefore = [Environment]::GetEnvironmentVariable('KICAD_USER_TEMPLATE_DIR', 'User')
$results = New-Object Collections.Generic.List[string]
$log = New-Object Collections.Generic.List[string]
function Check([string]$name, [bool]$ok, [string]$detail = '') { $results.Add(('{0}  {1}{2}' -f $(if ($ok) { 'PASS' } else { 'FAIL' }), $name, $(if ($detail) { "  ($detail)" } else { '' }))) }

# --- project copy (with codes imported through the command line)
if (Test-Path $Work) { Remove-Item -LiteralPath $Work -Recurse -Force }
New-Item -ItemType Directory -Force $Work | Out-Null
$srcDir = Split-Path (Resolve-Path $Project).Path -Parent
Get-ChildItem $srcDir -File | Where-Object { $_.Name -notlike '*.lck' } | Copy-Item -Destination $Work
$pro = Join-Path $Work (Split-Path $Project -Leaf)
# Codes normally come from the schematic ("LCSC Part"). To test the codes entered in the app, the first code found in the
# copy's schematic is cleared everywhere (that group then needs a code from the app / the imported file).
$schCopy = [IO.Path]::ChangeExtension($pro, '.kicad_sch')
$schText = [IO.File]::ReadAllText($schCopy)
$cleared = [regex]::Match($schText, '\(property "LCSC Part" "(C\d+)"').Groups[1].Value
if ($cleared) { [IO.File]::WriteAllText($schCopy, $schText.Replace('(property "LCSC Part" "' + $cleared + '"', '(property "LCSC Part" ""'), (New-Object Text.UTF8Encoding $false)) }
if ($Codes) { & $exe --layers 6 --project $pro --bom --import-codes $Codes | Out-Null }

# --- load the app and reach its internal types
$asm = [Reflection.Assembly]::LoadFrom($exe)
function T([string]$n) { $asm.GetType("KiCadDrc.$n", $true) }
$NP = [Reflection.BindingFlags]'NonPublic,Public,Instance,Static'
function Field($o, [string]$n) { $o.GetType().GetField($n, $NP).GetValue($o) }
function Call($o, [string]$n, [object[]]$a = @()) {
    $m = $o.GetType().GetMethods($NP) | Where-Object { $_.Name -eq $n -and $_.GetParameters().Count -eq $a.Count } | Select-Object -First 1
    # PowerShell wraps values in PSObject; reflection needs the plain .NET values
    $plain = [object[]]@($a | ForEach-Object { if ($_ -is [Management.Automation.PSObject]) { $_.PSObject.BaseObject } else { $_ } })
    $m.Invoke($o, $plain)
}
function Pump([int]$ms) { $sw = [Diagnostics.Stopwatch]::StartNew(); while ($sw.ElapsedMilliseconds -lt $ms) { [Windows.Forms.Application]::DoEvents(); [Threading.Thread]::Sleep(10) } }
function PumpUntil([scriptblock]$cond, [int]$max = 120000) { $sw = [Diagnostics.Stopwatch]::StartNew(); while (-not (& $cond) -and $sw.ElapsedMilliseconds -lt $max) { [Windows.Forms.Application]::DoEvents(); [Threading.Thread]::Sleep(20) }; & $cond }

$tx = T 'Tx'
$tx.GetField('Turkish').SetValue($null, $false)
$dialogs = T 'Dialogs'
$confirmAnswer = $true
$dialogs.GetField('Confirm').SetValue($null, [Func[Windows.Forms.IWin32Window, string, string, bool]] { param($o, $text, $title) $log.Add("CONFIRM [$title]"); $script:confirmAnswer })
$dialogs.GetField('Show').SetValue($null, [Action[Windows.Forms.IWin32Window, string, string, Windows.Forms.MessageBoxIcon]] { param($o, $text, $title, $icon) $log.Add("DIALOG [$title] $icon : " + ($text -split "`n")[0]) })

$env = [Activator]::CreateInstance((T 'KiCadEnvironment'))
$list = [Activator]::CreateInstance([Collections.Generic.List``1].MakeGenericType((T 'IManufacturer')))
$list.Add([Activator]::CreateInstance((T 'Jlc')))
function NewForm($start) {
    $f = [Activator]::CreateInstance((T 'MainForm'), [object[]]@($env, $list, $start))
    $f.StartPosition = 'Manual'; $f.Location = New-Object Drawing.Point(-4000, -4000)
    $f.Show(); Pump 700
    $f
}
function Banner($f) { (Field $f 'lBanner').Text }

try {
    # ================================================================ 1) language switch keeps the selection
    # A project is selected first: the rebuilt window reloads it but must not overwrite the choices on the left
    $f = NewForm $null
    Call $f 'SelectProject' @($pro, $false, $true)
    PumpUntil { (Field $f 'bom') -ne $null } 120000 | Out-Null
    $cLayers = Field $f 'cLayers'
    $i6 = 0; for ($k = 0; $k -lt $cLayers.Items.Count; $k++) { if ($cLayers.Items[$k].Value -eq 6) { $i6 = $k } }
    $cLayers.SelectedIndex = $i6; Pump 200
    (Field $f 'cMask').SelectedIndex = 6; Pump 200   # white
    (Field $f 'tabs').SelectedIndex = 1
    Call $f 'SwitchLanguage' @($true); Pump 300
    Check 'Language: window closes with restart request' ($f.RestartRequested -and $f.IsDisposed)
    $sel = $f.CurrentSelection
    Check 'Language: selection captured before closing' ($sel -ne $null -and $sel.Layers -eq 6 -and $sel.Mask -eq 'white') "layers=$($sel.Layers) mask=$($sel.Mask)"
    Check 'Language: tab captured' ($f.CurrentTab -eq 1)
    Check 'Language: Tx switched to Turkish' ([bool]$tx.GetField('Turkish').GetValue($null))
    Check 'Language: saved in settings.json' ((Get-Content (Join-Path $env.DataDir 'settings.json') -Raw) -match '"language":\s*"tr"')
    $f = NewForm $sel
    PumpUntil { (Field $f 'bom') -ne $null } 120000 | Out-Null
    Check 'Language: project reloaded in the new window' ((Field $f 'selectedProject') -ne $null)
    Check 'Language: new window in Turkish with same layers' ((Field $f 'cLayers').Text -eq '6 katman') (Field $f 'cLayers').Text
    Check 'Language: mask kept after restart' ((Field $f 'cMask').Text -like 'Beyaz*') (Field $f 'cMask').Text
    $f.Close(); Pump 200
    $tx.GetField('Turkish').SetValue($null, $false)

    # ================================================================ 2) production tab
    $f = NewForm $null
    Call $f 'SelectProject' @($pro, $true, $true)
    $loaded = PumpUntil { (Field $f 'bom') -ne $null } 120000
    $grid = Field $f 'gridBom'
    Check 'Production: BOM loaded into the grid' ($loaded -and $grid.Rows.Count -gt 0) "rows=$($grid.Rows.Count)"
    Check 'Production: left panel synced with project (6 layers)' ((Field $f 'cLayers').Text -eq '6 layers') (Field $f 'cLayers').Text
    $bom = Field $f 'bom'
    $fromSch = @($bom | Where-Object { $_.FromSchematic })
    Check 'Production: codes read from the schematic' ($fromSch.Count -gt 0 -and -not ($bom | Where-Object { $_.FromSchematic -and $_.Lcsc -eq $cleared })) "rows from schematic=$($fromSch.Count), cleared $cleared"
    $schRow = $grid.Rows | Where-Object { $_.Tag.FromSchematic } | Select-Object -First 1
    $schCode = $schRow.Cells['LCSC'].Value
    $schRow.Cells['LCSC'].Value = 'C1'; Pump 100
    Check 'Production: schematic code cannot be changed in the grid' ($schRow.Cells['LCSC'].Value -eq $schCode -and $schRow.Tag.Lcsc -eq $schCode) $schRow.Cells['LCSC'].Value
    $missing = @($bom | Where-Object { -not $_.Excluded -and $_.Lcsc -notmatch '^C\d{3,}$' })
    Check 'Production: Build disabled while codes are missing' (-not (Field $f 'bBuild').Enabled) "missing=$($missing.Count)"
    # untick "Place" for rows without a code (solder pads)
    foreach ($r in $grid.Rows) { if ($r.Tag.Lcsc -notmatch '^C\d{3,}$') { $r.Cells['Place'].Value = $false } }
    Pump 200
    Check 'Production: Build enabled after unticking Place' ((Field $f 'bBuild').Enabled) (Field $f 'lBomStatus').Text
    $codeFile = [IO.Path]::ChangeExtension($pro, '.lcsc.json')
    Check 'Production: do_not_place saved' ((Get-Content $codeFile -Raw) -match '"do_not_place": \[\s*"')
    # the cleared group is placed again with its code typed in the app (when -Codes did not fill it already)
    $typed = $grid.Rows | Where-Object { $_.Tag.Lcsc -notmatch '^C\d{3,}$' -and -not $_.Tag.FromSchematic } | Select-Object -First 1
    if ($typed -and $cleared) { $typed.Cells['Place'].Value = $true; $typed.Cells['LCSC'].Value = $cleared; Pump 200 }
    $typedRow = $bom | Where-Object { -not $_.FromSchematic -and $_.Lcsc -eq $cleared } | Select-Object -First 1
    Check 'Production: code typed in the app is used' ($typedRow -and -not $typedRow.Excluded -and (Field $f 'bBuild').Enabled) (Field $f 'lBomStatus').Text
    # invalid code -> red, build disabled; then restored
    $row = $grid.Rows | Where-Object { $_.Tag.Lcsc -match '^C\d{3,}$' -and -not $_.Tag.FromSchematic } | Select-Object -First 1
    $old = $row.Cells['LCSC'].Value
    $row.Cells['LCSC'].Value = 'x12'; Pump 200
    Check 'Production: invalid code disables Build' (-not (Field $f 'bBuild').Enabled) (Field $f 'lBomStatus').Text
    $row.Cells['LCSC'].Value = ' ' + $old.ToLower(); Pump 200
    Check 'Production: code is trimmed and upper-cased' ($row.Cells['LCSC'].Value -eq $old -and (Field $f 'bBuild').Enabled) $row.Cells['LCSC'].Value
    (Field $f 'nBoards').Value = 10
    $log.Clear(); $confirmAnswer = $false
    Call $f 'BuildProduction'; Pump 300
    Check 'Production: "No" in the confirmation does nothing' (-not (Field $f 'busy') -and -not (Test-Path (Join-Path $Work 'JLC_Production')))
    $confirmAnswer = $true
    Call $f 'BuildProduction'; Pump 200
    Check 'Production: grid read-only while building' ($grid.ReadOnly -and -not (Field $f 'bImport').Enabled)
    PumpUntil { -not (Field $f 'busy') } 300000 | Out-Null
    $files = @(Get-ChildItem (Join-Path $Work 'JLC_Production') -File | ForEach-Object Name)
    Check 'Production: five files created' ($files.Count -eq 5) ($files -join ', ')
    Check 'Production: success banner' ((Banner $f) -match '^(✓|⚠)') (Banner $f)
    Check 'Production: grid editable again' (-not $grid.ReadOnly -and -not $grid.Columns['LCSC'].ReadOnly -and $grid.Columns['Designator'].ReadOnly)

    # ================================================================ 3) apply to the selected project
    $log.Clear()
    Call $f 'ApplyToProject'
    Check 'Project: confirmation asked' (($log -join '|') -match 'CONFIRM')
    PumpUntil { -not (Field $f 'busy') } 600000 | Out-Null
    Check 'Project: updated banner' ((Banner $f) -match 'updated') (Banner $f)
    $dru = Get-Content ([IO.Path]::ChangeExtension($pro, '.kicad_dru')) -Raw
    Check 'Project: single English rule block' (([regex]::Matches($dru, 'KiCad DRC Configurator: begin')).Count -eq 1 -and $dru -notmatch 'Ayarlay')
    Check 'Project: backup in Data\Project Backups' (@(Get-ChildItem (Join-Path $env.DataDir 'Project Backups') -Directory).Count -ge 1)

    # ================================================================ 4) apply template + verification, then restore defaults
    Call $f 'Apply'
    Check 'Apply: busy during verification' ([bool](Field $f 'busy'))
    PumpUntil { -not (Field $f 'busy') } 180000 | Out-Null
    Check 'Apply: verified banner' ((Banner $f) -match 'verified') (Banner $f)
    Check 'Apply: KiCad template variable points to test Data' ([Environment]::GetEnvironmentVariable('KICAD_USER_TEMPLATE_DIR', 'User') -eq (Join-Path $env.DataDir 'Templates'))
    # language switch is refused while busy
    Call $f 'Apply'; $log.Clear()
    Call $f 'SwitchLanguage' @($true); Pump 100
    Check 'Language: refused while busy' (-not $f.RestartRequested -and ($log -join '|') -match 'DIALOG')
    PumpUntil { -not (Field $f 'busy') } 180000 | Out-Null
    Call $f 'RestoreDefaults'; Pump 300
    Check 'Restore defaults: template removed, state deleted' (-not (Test-Path (Join-Path $env.DataDir 'Templates\DRC_JLC_6L')) -and -not (Test-Path $env.StatePath))

    # ================================================================ 5) PCB calculators tab (calculator 1)
    $tabs = Field $f 'tabs'
    $tabs.SelectedTab = (Field $f 'calcPage'); Pump 400
    Check 'Calculators: settings panel hidden' (-not (Field $f 'settingsPanel').Visible)
    $bottomBar = (Field $f 'bApply').Parent
    Check 'Bottom bar hidden on calculators tab' (-not $bottomBar.Visible)
    $tabs.SelectedIndex = 2; Pump 150
    Check 'Bottom bar hidden on production tab' (-not $bottomBar.Visible)
    $tabs.SelectedIndex = 1; Pump 150
    Check 'Bottom bar shown on stackup tab' ($bottomBar.Visible)
    $tabs.SelectedTab = (Field $f 'calcPage'); Pump 300
    (Field $f 'calcList').SelectedIndex = 0; Pump 300
    $view = (Field $f 'calcHostPanel').Controls[0]
    function AllControls($c) { foreach ($x in $c.Controls) { $x; AllControls $x } }
    $resultBack = [Drawing.Color]::FromArgb(243, 246, 252)
    $boxes = @(AllControls $view | Where-Object { $_ -is [Windows.Forms.TextBox] })
    $outputs = @($boxes | Where-Object { $_.ReadOnly -and $_.BackColor.ToArgb() -eq $resultBack.ToArgb() })
    $inputs = @($boxes | Where-Object { $outputs -notcontains $_ })
    Check 'Calculators: 9 result boxes' ($outputs.Count -eq 9) "results=$($outputs.Count)"
    Check 'Calculators: bandwidth for 1 ns' ($outputs[0].Text -eq '350 MHz') $outputs[0].Text
    $inputs[0].Text = '2'; Pump 100
    Check 'Calculators: live update (2 ns -> 175 MHz)' ($outputs[0].Text -eq '175 MHz') $outputs[0].Text
    $inputs[0].Text = 'abc'; Pump 100
    Check 'Calculators: invalid input is red and gives no result' ($inputs[0].BackColor.ToArgb() -ne [Drawing.SystemColors]::Window.ToArgb() -and $outputs[5].Text -eq '—') $outputs[5].Text
    $inputs[0].Text = '1'; Pump 100
    $trace = $inputs | Where-Object { $_.Text -eq '' } | Select-Object -First 1
    $trace.Text = '500'; Pump 100
    $verdict = AllControls $view | Where-Object { $_ -is [Windows.Forms.Label] -and $_.Text -like '⚠*' } | Select-Object -First 1
    Check 'Calculators: long trace gives a warning' ($verdict -ne $null) $(if ($verdict) { $verdict.Text.Substring(0, 40) })
    # units: imperial shows inches first
    (Field $f 'calcImperial').Checked = $true; Pump 150
    Check 'Calculators: imperial units' ($outputs[5].Text -like '*in  (*mm)') $outputs[5].Text
    (Field $f 'calcMetric').Checked = $true; Pump 150
    # the stackup changes on the left while another tab is open: inputs stay, the material list follows the new stackup
    $inputs[0].Text = '2'; Pump 100
    $tabs.SelectedIndex = 0; Pump 200
    Check 'Calculators: settings panel back on other tabs' ((Field $f 'settingsPanel').Visible)
    $cl = Field $f 'cLayers'; for ($k = 0; $k -lt $cl.Items.Count; $k++) { if ($cl.Items[$k].Value -eq 4) { $cl.SelectedIndex = $k } }; Pump 300
    $tabs.SelectedTab = (Field $f 'calcPage'); Pump 300
    $viewAfter = (Field $f 'calcHostPanel').Controls[0]
    $combo = AllControls $viewAfter | Where-Object { $_ -is [Windows.Forms.ComboBox] -and $_.Items.Count -gt 40 } | Select-Object -First 1
    Check 'Calculators: same view kept, input kept' ($viewAfter -eq $view -and $inputs[0].Text -eq '2') $inputs[0].Text
    Check 'Calculators: material list follows the new stackup' ($combo.Items[0].ToString() -like '*JLC04*' -and $combo.SelectedIndex -eq 0) $combo.Items[0].ToString()
    # "How it works" page
    (Field $f 'calcModeGuide').PerformClick(); Pump 300
    $guide = (Field $f 'calcHostPanel').Controls[0]
    $labels = @(AllControls $guide | Where-Object { $_ -is [Windows.Forms.Label] }).Count
    $figures = @(AllControls $guide | Where-Object { $_.GetType().Name -eq 'Figure' }).Count
    Check 'Calculators: guide page with text and figures' ($guide -ne $view -and $labels -gt 40 -and $figures -ge 6) "labels=$labels figures=$figures"
    $slider = AllControls $guide | Where-Object { $_ -is [Windows.Forms.TrackBar] } | Select-Object -First 1
    $slider.Value = 24; Pump 150
    $ring = AllControls $guide | Where-Object { $_ -is [Windows.Forms.Label] -and $_.Text -like '⚠*54*' } | Select-Object -First 1
    $slider.Value = 4; Pump 150
    $clean = AllControls $guide | Where-Object { $_ -is [Windows.Forms.Label] -and $_.Text -like '✓*' } | Select-Object -First 1
    Check 'Calculators: try-it demo (120 mm rings 54 %, 20 mm clean)' ($ring -ne $null -and $clean -ne $null)
    (Field $f 'calcModeCalc').PerformClick(); Pump 200
    Check 'Calculators: back to the same calculator view' ((Field $f 'calcHostPanel').Controls[0] -eq $view)
    # smallest window: nothing throws, the list gives way
    $f.WindowState = 'Normal'; $f.Size = $f.MinimumSize; Pump 300
    $bmp = New-Object Drawing.Bitmap $f.Width, $f.Height
    $f.DrawToBitmap($bmp, [Drawing.Rectangle]::new(0, 0, $f.Width, $f.Height)); $bmp.Dispose()
    Check 'Calculators: list narrower in a small window' ((Field $f 'calcList').Width -lt 330) "list=$((Field $f 'calcList').Width)"
    # language switch while the calculator tab is open: the tab is kept
    Call $f 'SwitchLanguage' @($true); Pump 200
    Check 'Calculators: language switch keeps the calculators tab' ($f.CurrentTab -eq 3) "tab=$($f.CurrentTab)"
    $tx.GetField('Turkish').SetValue($null, $false)
}
catch { $results.Add('FAIL  script error: ' + $_.Exception.GetBaseException().Message) }
finally {
    [Environment]::SetEnvironmentVariable('KICAD_USER_TEMPLATE_DIR', $envBefore, 'User')
}
$results
"dialogs: " + ($log -join ' | ')
$failed = @($results | Where-Object { $_ -like 'FAIL*' }).Count
"TOTAL: " + ($results.Count - $failed) + " / " + $results.Count + " checks passed"
