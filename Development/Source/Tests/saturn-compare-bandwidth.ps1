# Parallel check of calculator 1 (Bandwidth & max conductor length) against Saturn PCB Toolkit 8.47.
# Saturn is driven through Tools\saturn-driver.ps1; our numbers come from BandwidthMath in the built exe.
# Our microstrip formula uses the published 0.475*Er + 0.67; Saturn uses 0.457*Er + 0.67. Every case is computed both ways:
# "compat" (0.457) must equal Saturn to its display precision, "ours" shows the effect of the published coefficient.
#   powershell -ExecutionPolicy Bypass -File saturn-compare-bandwidth.ps1 -Exe "..\..\..\KiCad DRC.exe" [-Report file.txt]
param([Parameter(Mandatory = $true)][string]$Exe, [string]$Report)
$ErrorActionPreference = 'Stop'
. (Join-Path (Split-Path $PSScriptRoot -Parent) 'Tools\saturn-driver.ps1')
$asm = [Reflection.Assembly]::LoadFrom((Resolve-Path $Exe).Path)
$M = $asm.GetType('KiCadDrc.BandwidthMath', $true)
function Mcall([string]$n, [object[]]$a) { ($M.GetMethods() | Where-Object { $_.Name -eq $n -and $_.GetParameters().Count -eq $a.Count } | Select-Object -First 1).Invoke($null, $a) }
$A475 = $M.GetField('MicrostripA').GetValue($null); $A457 = $M.GetField('SaturnMicrostripA').GetValue($null)
$inch = 0.0254

Start-Saturn | Out-Null
Select-SaturnTab 'Bandwidth && Max Conductor Length' | Out-Null
$ctl = Get-SaturnControls -VisibleOnly
function Find-Ctl([string]$cls, [int]$x, [int]$y) { ($ctl | Where-Object { $_.Class -eq $cls -and [math]::Abs($_.X - $x) -le 3 -and [math]::Abs($_.Y - $y) -le 3 } | Select-Object -First 1).Handle }
$c = @{ Rise = (Find-Ctl 'TEdit' 49 288); RiseMode = (Find-Ctl 'TGroupButton' 41 177); FreqMode = (Find-Ctl 'TGroupButton' 41 211); Imperial = (Find-Ctl 'TGroupButton' 790 168)
        Combo = (Find-Ctl 'TComboBox' 791 264); Er = (Find-Ctl 'TEdit' 791 312); BW = (Find-Ctl 'TEdit' 49 432); Speed = (Find-Ctl 'TEdit' 49 496); Lambda = (Find-Ctl 'TEdit' 337 464)
        SrTrack = (Find-Ctl 'TTrackBar' 41 560); LTrack = (Find-Ctl 'TTrackBar' 332 560); Micro = (Find-Ctl 'TGroupButton' 623 569); Strip = (Find-Ctl 'TGroupButton' 623 599)
        Solve = (Find-Ctl 'TButton' 871 599); MaxIpc = (Find-Ctl 'TEdit' 49 639); MaxFreq = (Find-Ctl 'TEdit' 337 639) }
foreach ($k in @($c.Keys)) { if (-not $c[$k]) { throw "Saturn control not found: $k (Saturn layout changed?)" } }
Invoke-SaturnClick $c.Imperial; Invoke-SaturnClick $c.RiseMode
$materials = Get-SaturnComboItems $c.Combo

# Saturn multiplies by 1/n rounded to 6 digits (1/7 -> 0.142857)
function SaturnDivide([double]$x, [double]$n) { $x * [math]::Round(1 / $n, 6) }
function Num([string]$s) { [double]([regex]::Match($s, '-?[\d.]+').Value) }
$lines = New-Object Collections.Generic.List[string]
$worst = 0.0; $n = 0; $fail = 0
function Add-Check([string]$case, [string]$what, [double]$saturn, [double]$compat, [double]$ours, [int]$decimals) {
    $script:n++
    $tol = [math]::Pow(10, -$decimals) * 0.51 + [math]::Abs($saturn) * 1e-9
    $ok = [math]::Abs($compat - $saturn) -le $tol
    if (-not $ok) { $script:fail++ }
    $delta = if ($saturn -ne 0) { ($ours - $saturn) / $saturn * 100 } else { 0 }
    if ([math]::Abs($delta) -gt $script:worst) { $script:worst = [math]::Abs($delta) }
    $lines.Add(('{0,-4} {1,-44} {2,-20} {3,14} {4,14} {5,14} {6,8}' -f $(if ($ok) { 'OK' } else { 'FAIL' }), $case, $what,
        $saturn.ToString('0.00000', [Globalization.CultureInfo]::InvariantCulture), $compat.ToString('0.00000', [Globalization.CultureInfo]::InvariantCulture),
        $ours.ToString('0.00000', [Globalization.CultureInfo]::InvariantCulture), $delta.ToString('+0.00;-0.00;0.00', [Globalization.CultureInfo]::InvariantCulture) + '%'))
}

# ---- rise time input: materials x circuits x rise times (Sr 0.25, lambda 1/7), then every factor/divisor once
$cases = @()
foreach ($mat in 'FR-4 STD', 'RO4350B', 'RO3010', 'Teflon PTFE', 'Air') { foreach ($circuit in 'Micro', 'Strip') { foreach ($tr in 0.1, 1, 2.5, 10) { $cases += , @($mat, $circuit, $tr, 1, 1) } } }
for ($s = 0; $s -le 6; $s++) { $cases += , @('FR-4 STD', 'Micro', 1, $s, 1) }
for ($l = 0; $l -le 3; $l++) { $cases += , @('FR-4 STD', 'Strip', 0.5, 1, $l) }
foreach ($cs in $cases) {
    $mat, $circuit, $tr, $srPos, $lPos = $cs
    Set-SaturnCombo $c.Combo ([array]::IndexOf($materials, $mat)); Invoke-SaturnClick $c[$circuit]
    Set-SaturnTrack $c.SrTrack $srPos; Set-SaturnTrack $c.LTrack $lPos
    Set-SaturnText $c.Rise ([string]$tr); Invoke-SaturnClick $c.Solve
    $er = Num (Get-SaturnText $c.Er)
    $micro = $circuit -eq 'Micro'
    $trS = $tr * 1e-9
    $factor = $M.GetField('SrFactors').GetValue($null)[$srPos]; $div = $M.GetField('LambdaDivisors').GetValue($null)[$lPos]
    $name = '{0}, {1}, tr {2} ns, {3} Sr, 1/{4}' -f $mat, $(if ($micro) { 'microstrip' } else { 'stripline' }), $tr, $factor, $div
    $f = Mcall 'BandwidthFromRise' @($trS)
    Add-Check $name 'bandwidth MHz' (Num (Get-SaturnText $c.BW)) ($f / 1e6) ($f / 1e6) 5
    $v = (Mcall 'Speed' @($er))
    Add-Check $name 'speed c/sqrt(Er) m/s' (Num (Get-SaturnText $c.Speed)) $v $v 3
    $eCompat = Mcall 'ErEffective' @($er, $micro, $A457); $eOurs = Mcall 'ErEffective' @($er, $micro, $A475)
    Add-Check $name 'max length IPC in' (Num (Get-SaturnText $c.MaxIpc)) ((Mcall 'MaxLengthIpc' @($trS, $eCompat, $factor)) / $inch) ((Mcall 'MaxLengthIpc' @($trS, $eOurs, $factor)) / $inch) 5
    Add-Check $name 'wavelength air in' (Num (Get-SaturnText $c.Lambda)) ((Mcall 'WavelengthAir' @($f)) / $inch) ((Mcall 'WavelengthAir' @($f)) / $inch) 5
    Add-Check $name 'max length freq in' (Num (Get-SaturnText $c.MaxFreq)) ((SaturnDivide (Mcall 'WavelengthAir' @($f)) $div) / $inch) ((Mcall 'MaxLengthFrequency' @($f, [double]$div)) / $inch) 5
}

# ---- frequency input (MHz)
Invoke-SaturnClick $c.FreqMode; Set-SaturnTrack $c.LTrack 1
foreach ($mhz in 1, 25, 100, 1000, 5000) {
    Set-SaturnText $c.Rise ([string]$mhz); Invoke-SaturnClick $c.Solve
    $f = $mhz * 1e6
    Add-Check ('frequency input ' + $mhz + ' MHz, 1/7') 'wavelength air in' (Num (Get-SaturnText $c.Lambda)) ((Mcall 'WavelengthAir' @($f)) / $inch) ((Mcall 'WavelengthAir' @($f)) / $inch) 5
    Add-Check ('frequency input ' + $mhz + ' MHz, 1/7') 'max length freq in' (Num (Get-SaturnText $c.MaxFreq)) ((SaturnDivide (Mcall 'WavelengthAir' @($f)) 7) / $inch) ((Mcall 'MaxLengthFrequency' @($f, 7.0)) / $inch) 5
}
Invoke-SaturnClick $c.RiseMode; Set-SaturnText $c.Rise '1'; Set-SaturnCombo $c.Combo 0; Invoke-SaturnClick $c.Micro; Set-SaturnTrack $c.SrTrack 1; Invoke-SaturnClick $c.Solve

$head = @('Calculator 1 - Bandwidth & max conductor length: parallel check against Saturn PCB Toolkit 8.47',
    ('Date: ' + (Get-Date -Format 'yyyy-MM-dd HH:mm')), '',
    'Formulas (see Calculators\Bandwidth\BandwidthMath.cs):',
    '  bandwidth f = 0.35 / tr;  v = c / sqrt(Er_eff);  Sr = tr * v;  L_ipc = k * Sr;  lambda = c / f;  L_freq = lambda / n',
    '  stripline Er_eff = Er;  microstrip Er_eff = 0.475 * Er + 0.67 (Motorola MECL handbook / IPC closed form)',
    '  Saturn uses 0.457 * Er + 0.67 for microstrip (digits swapped). Column "compat" recomputes with 0.457 and must match Saturn;',
    '  column "ours" uses 0.475; delta = ours vs Saturn. Only the microstrip IPC-2251 length differs.',
    '  Saturn also multiplies the wavelength by 1/n rounded to 6 digits (1/7 = 0.142857); ours divides exactly (difference < 0.0002 %).', '',
    ('{0,-4} {1,-44} {2,-20} {3,14} {4,14} {5,14} {6,8}' -f '', 'case', 'value', 'Saturn', 'compat', 'ours', 'delta'))
$tail = @('', ('TOTAL: {0} / {1} values match Saturn (compat)   largest difference of our published formula: {2} %' -f ($n - $fail), $n, $worst.ToString('0.00', [Globalization.CultureInfo]::InvariantCulture)))
$all = $head + $lines + $tail
if ($Report) { [IO.File]::WriteAllLines($Report, $all, (New-Object Text.UTF8Encoding $true)) }
$tail
