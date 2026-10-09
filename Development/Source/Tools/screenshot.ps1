# Renders the main window off screen and saves a screenshot (calculators tab), for layout checks.
#   powershell -ExecutionPolicy Bypass -File screenshot.ps1 -Exe "<exe>" -Out file.png [-Lang tr|en] [-Guide] [-Slider N] [-Scroll px]
#       [-W 1500 -H 950] [-Trace 60] [-Stripline]        simulate 150 % scaling: $env:KICAD_DRC_TEST_DPI = '1.5'
param([string]$Exe, [string]$Out, [string]$Lang = 'tr', [switch]$Guide, [int]$W = 1500, [int]$H = 950, [string]$Trace = '', [switch]$Stripline, [int]$Scroll = 0, [int]$Slider = 0)
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
[Globalization.CultureInfo]::DefaultThreadCurrentCulture = [Globalization.CultureInfo]::InvariantCulture
$asm = [Reflection.Assembly]::LoadFrom($Exe)
function T([string]$n) { $asm.GetType("KiCadDrc.$n", $true) }
$NP = [Reflection.BindingFlags]'NonPublic,Public,Instance,Static'
function Field($o, [string]$n) { $o.GetType().GetField($n, $NP).GetValue($o) }
function Pump([int]$ms) { $sw = [Diagnostics.Stopwatch]::StartNew(); while ($sw.ElapsedMilliseconds -lt $ms) { [Windows.Forms.Application]::DoEvents(); [Threading.Thread]::Sleep(10) } }
(T 'Tx').GetField('Turkish').SetValue($null, $Lang -eq 'tr')
$env = [Activator]::CreateInstance((T 'KiCadEnvironment'))
$list = [Activator]::CreateInstance([Collections.Generic.List``1].MakeGenericType((T 'IManufacturer'))); $list.Add([Activator]::CreateInstance((T 'Jlc')))
$j = T 'Jlc'; $jl = $list[0]
$sel = $jl.Recommended(6, [double]::NaN, [double]::NaN, [double]::NaN)
$f = [Activator]::CreateInstance((T 'MainForm'), [object[]]@($env, $list, $sel))
$f.StartPosition = 'Manual'; $f.Location = New-Object Drawing.Point(-5000, -5000); $f.Size = New-Object Drawing.Size($W, $H)
$f.Show(); Pump 500
$tabs = Field $f 'tabs'; $tabs.SelectedTab = (Field $f 'calcPage'); Pump 400
(Field $f 'calcList').SelectedIndex = 0; Pump 200
if ($Guide) { (Field $f 'calcModeGuide').PerformClick(); Pump 500; if ($Slider -gt 0) { function AllC($c) { foreach ($x in $c.Controls) { $x; AllC $x } }; (AllC (Field $f 'calcHostPanel').Controls[0] | Where-Object { $_ -is [Windows.Forms.TrackBar] } | Select-Object -First 1).Value = $Slider; Pump 300 } }
else {
  $view = (Field $f 'calcHostPanel').Controls[0]
  function AllControls($c) { foreach ($x in $c.Controls) { $x; AllControls $x } }
  if ($Stripline) { (AllControls $view | Where-Object { $_ -is [Windows.Forms.RadioButton] -and $_.Text -eq 'Stripline' }).Checked = $true }
  if ($Trace) { ($AllTb = AllControls $view | Where-Object { $_ -is [Windows.Forms.TextBox] -and -not $_.ReadOnly -and $_.Text -eq '' } | Select-Object -First 1).Text = $Trace }
  Pump 300
}
if ($Scroll -gt 0) { $p = (Field $f 'calcHostPanel').Controls[0]; $p.AutoScrollPosition = New-Object Drawing.Point(0, $Scroll); Pump 300 }
$bmp = New-Object Drawing.Bitmap $f.Width, $f.Height
$f.DrawToBitmap($bmp, (New-Object Drawing.Rectangle 0, 0, $f.Width, $f.Height)); $bmp.Save($Out)
$f.Close()
