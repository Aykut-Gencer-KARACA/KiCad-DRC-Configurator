# Unit tests of the app's pure functions (no KiCad, no network): file names, price tiers, CSV parsing, S-expressions,
# .kicad_dru block replacement, legacy name mapping, JSON writing, English plurals.
#   powershell -ExecutionPolicy Bypass -File unit-tests.ps1 -Exe "KiCad DRC.exe"
param([Parameter(Mandatory = $true)][string]$Exe)
$ErrorActionPreference = 'Stop'
[Globalization.CultureInfo]::DefaultThreadCurrentCulture = [Globalization.CultureInfo]::InvariantCulture
$asm = [Reflection.Assembly]::LoadFrom((Resolve-Path $Exe).Path)
function T([string]$n) { $asm.GetType("KiCadDrc.$n", $true) }
$NP = [Reflection.BindingFlags]'NonPublic,Public,Static,Instance'
function SCall([string]$type, [string]$name, [object[]]$a) {
    $m = (T $type).GetMethods($NP) | Where-Object { $_.Name -eq $name -and $_.GetParameters().Count -eq $a.Count } | Select-Object -First 1
    $m.Invoke($null, $a)
}
$results = New-Object Collections.Generic.List[string]
function Eq([string]$name, $actual, $expected) {
    $ok = "$actual" -ceq "$expected"
    $results.Add(('{0}  {1}{2}' -f $(if ($ok) { 'PASS' } else { 'FAIL' }), $name, $(if ($ok) { '' } else { "  (got [$actual], expected [$expected])" })))
}
(T 'Tx').GetField('Turkish').SetValue($null, $false)

# ---- production file names from the revision
$pi = [Activator]::CreateInstance((T 'ProjectInfo'))
$pi.Name = 'Board'; $pi.Dir = 'C:\x'
foreach ($c in @(@('C', 'Board_RevC'), @('RevB', 'Board_RevB'), @('rev 2', 'Board_Rev2'), @('1.0', 'Board_Rev1.0'), @('—', 'Board'), @('A/B*', 'Board_RevAB'), @('', 'Board'))) {
    $pi.Revision = $c[0]; Eq ("FileBase rev '" + $c[0] + "'") (SCall 'Production' 'FileBase' @($pi)) $c[1]
}

# ---- price tiers
$part = [Activator]::CreateInstance((T 'PartInfo'))
$part.Prices.Add([double[]]@(1, 9, 1.0)); $part.Prices.Add([double[]]@(10, 99, 0.5)); $part.Prices.Add([double[]]@(100, 499, 0.2))
Eq 'UnitPrice 5' ($part.UnitPrice(5)) 1
Eq 'UnitPrice 10' ($part.UnitPrice(10)) 0.5
Eq 'UnitPrice 499' ($part.UnitPrice(499)) 0.2
Eq 'UnitPrice above all closed tiers uses the highest' ($part.UnitPrice(5000)) 0.2
$part.Prices.Add([double[]]@(500, -1, 0.1))
Eq 'UnitPrice open-ended tier' ($part.UnitPrice(5000)) 0.1
$empty = [Activator]::CreateInstance((T 'PartInfo'))
Eq 'UnitPrice without tiers is NaN' ([double]::IsNaN($empty.UnitPrice(3))) $true

# ---- JLC charged quantity (attrition, minimum quantity). Expected values are answers of JLC's live calculator
# .../smtGood/calculateComponentOrderQty recorded on 2026-10-08 (1080/1080 rows equal the formula, also above the reel size)
function NewPart([int]$loss, [int]$least, [int]$reel) {
    $x = [Activator]::CreateInstance((T 'PartInfo')); $x.Loss = $loss; $x.LeastPatch = $least; $x.Reel = $reel; $x
}
foreach ($c in @(@('single', 1, 20, 10, 20, 4000, 30), @('single', 1, 2, 4, 15, 2000, 15), @('single', 10, 2, 4, 15, 2000, 24),
        @('single', 10, 6, 0, 0, 5000, 60), @('both', 5, 3, 10, 20, 4000, 35), @('both', 10, 4, 4, 5, 2000, 48), @('both', 5, 1, 0, 0, 160, 5),
        @('single', 1000, 20, 10, 20, 4000, 20042), @('both', 1000, 20, 10, 20, 4000, 20084), @('single', 5000, 7, 2, 5, 160, 35071))) {
    $a = @((NewPart $c[3] $c[4] $c[5]), [int]$c[2], [int]$c[1], ($c[0] -eq 'both'), $false)
    Eq ("ChargedQty " + ($c[0..5] -join ' ')) (SCall 'JlcCost' 'ChargedQty' $a) $c[6]
}
$a = @((NewPart -1 -1 -1), 3, 10, $false, $true)
Eq 'ChargedQty without attrition data is the need' (SCall 'JlcCost' 'ChargedQty' $a) 30
Eq 'ChargedQty without attrition data reports it' $a[4] $false

# ---- JLC cost estimate: price tier by the charged quantity, fees of Economic (one side) / Standard (both sides)
function NewRow([string]$code, [int]$qty) { $r = [Activator]::CreateInstance((T 'BomRow')); $r.Lcsc = $code; $r.Qty = $qty; $r.Value = $code; $r.Footprint = 'x'; $r }
$basic = NewPart 10 20 4000; $basic.Type = 'Basic'; $basic.Stock = 15
$basic.Prices.Add([double[]]@(1, 19, 1.0)); $basic.Prices.Add([double[]]@(20, -1, 0.5))
$ext = NewPart 0 0 4000; $ext.Type = 'Extended'; $ext.Stock = 1000; $ext.Prices.Add([double[]]@(1, -1, 2.0))
$info = [Activator]::CreateInstance(([Collections.Generic.Dictionary``2].MakeGenericType([string], (T 'PartInfo'))))
$info['C1001'] = $basic; $info['C2002'] = $ext
$placed = [Activator]::CreateInstance(([Collections.Generic.List``1].MakeGenericType((T 'BomRow'))))
$placed.Add((NewRow 'C1001' 1)); $placed.Add((NewRow 'C2002' 2)); $placed.Add((NewRow 'C9999' 1))
$e = SCall 'JlcCost' 'Estimate' @($placed, $info, 1, $false, 100, 4, 0)
Eq 'Estimate charges the minimum quantity' $e.Lines[0].Charged 20
Eq 'Estimate uses the tier of the charged quantity' $e.Lines[0].Unit 0.5
Eq 'Estimate low stock against the charged quantity' $e.Lines[0].LowStock $true
Eq 'Estimate parts total' ([math]::Round($e.PartsTotal, 4)) 14
Eq 'Estimate counts a part without data as unpriced' $e.Unpriced 1
Eq 'Estimate Economic: loading only for Extended kinds' $e.LoadingKinds 1
Eq 'Estimate Economic fees (THT: joints + hand-soldering labour)' ([math]::Round($e.AssemblyTotal, 4)) ([math]::Round(8.18 + 1.53 + 3.07 + 100 * 0.0016 + 4 * 0.0164 + 3.58, 4))
$e = SCall 'JlcCost' 'Estimate' @($placed, $info, 5, $true, 100, 0, 8)
Eq 'Estimate both sides doubles attrition (live calculator: 25)' $e.Lines[0].Charged 25
Eq 'Estimate Standard: loading for every kind' $e.LoadingKinds 2
Eq 'Estimate X-ray: 40 leadless parts at the 11-50 tier' ([math]::Round($e.Xray, 4)) 32.8
Eq 'Estimate Standard fees (X-ray, packing, no hand labour)' ([math]::Round($e.AssemblyTotal, 4)) ([math]::Round(2 * 25.56 + 2 * 8.21 + 2 * 1.53 + 500 * 0.0016 + 40 * 0.82 + 0.5, 4))
Eq 'Joint tiers: Standard 60,000 SMT joints' (SCall 'JlcCost' 'Rate' @((T 'JlcCost').GetField('StandardSmtJoint').GetValue($null), [long]60000)) 0.0013
Eq 'Joint tiers: manual 10,000 is still the first tier' (SCall 'JlcCost' 'Rate' @((T 'JlcCost').GetField('ManualJoint').GetValue($null), [long]10000)) 0.0164
foreach ($c in @(@('EasyEDA:QFN-24_L4.0-W4.0-P0.50-BL-EP2.7', $true), @('EasyEDA:DFN-8_L5.8-W4.9-P1.27-LS6.1-BL-2', $true),
        @('Package_SON:WSON-8-1EP_6x5mm', $true), @('Package_BGA:BGA-64', $true), @('Package_SO:TSSOP-20_4.4x6.5mm', $false),
        @('Package_LCC:PLCC-44', $false), @('Package_TO_SOT_SMD:SOT-23', $false), @('Diode_SMD:D_SMB', $false))) {
    Eq ("IsLeadless " + $c[0]) (SCall 'JlcCost' 'IsLeadless' @($c[0])) $c[1]
}

# ---- CSV (RFC 4180)
$rows = SCall 'Production' 'Csv' @("a,b,c`r`n""x,1"",""say """"hi"""""",`r`n""multi`nline"",2,3")
Eq 'Csv row count' $rows.Count 3
Eq 'Csv quoted comma' $rows[1][0] 'x,1'
Eq 'Csv escaped quote' $rows[1][1] 'say "hi"'
Eq 'Csv empty last field' $rows[1][2] ''
Eq 'Csv newline inside quotes' $rows[2][0] "multi`nline"

# ---- CPL for JLC: rotation convention (top as KiCad, bottom 180 - angle, 0 <= r < 360)
foreach ($c in @(@(0, $false, 0), @(-90, $false, 270), @(360, $false, 0), @(90, $false, 90), @(0, $true, 180), @(180, $true, 0),
                 @(90, $true, 90), @(-90, $true, 270), @(165, $true, 15), @(-180, $true, 0), @(45.5, $true, 134.5))) {
    Eq ("JlcRotation " + $c[0] + $(if ($c[1]) { ' bottom' } else { ' top' })) (SCall 'Production' 'JlcRotation' @([double]$c[0], [bool]$c[1])) $c[2]
}
# ---- CPL keeps only placed BOM parts; board-only footprints are reported, parts missing on the board too
function Row([string]$type, [hashtable]$v) { $o = [Activator]::CreateInstance((T $type)); foreach ($k in $v.Keys) { $o.GetType().GetField($k).SetValue($o, $v[$k]) }; $o }
$cplList = [Activator]::CreateInstance([Collections.Generic.List``1].MakeGenericType((T 'CplRow')))
foreach ($r in 'R1', 'R2', 'J1', 'REF**', '') { $cplList.Add((Row 'CplRow' @{ Ref = $r })) }
$bomList = [Activator]::CreateInstance([Collections.Generic.List``1].MakeGenericType((T 'BomRow')))
$bomList.Add((Row 'BomRow' @{ Refs = 'R1,R2,R3' })); $bomList.Add((Row 'BomRow' @{ Refs = 'J1'; Excluded = $true }))
$leftOut = [Collections.Generic.List[string]]::new(); $notOnBoard = [Collections.Generic.List[string]]::new()
$kept = SCall 'Production' 'CplForBom' @($cplList, $bomList, $leftOut, $notOnBoard)
Eq 'CplForBom keeps placed parts only' (($kept | ForEach-Object Ref) -join ',') 'R1,R2'
Eq 'CplForBom reports board-only footprints' ($leftOut -join ',') 'REF**,(no reference)'
Eq 'CplForBom reports placed parts missing on the board' ($notOnBoard -join ',') 'R3'

# ---- JLC placement like the Fabrication Toolkit: corrections only for KiCad library footprints, never for EasyEDA / LCSC ones
# polarised capacitors are never turned (their JLC orientation depends on the LCSC part: -FD forward / -RD reversed)
foreach ($c in @(@('Capacitor_SMD:CP_Elec_6.3x7.7', 0), @('Capacitor_Tantalum_SMD:CP_EIA-3216-18_Kemet-A', 0), @('Package_QFP:LQFP-64_10x10mm_P0.5mm', 270), @('Package_TO_SOT_SMD:SOT-23', 180),
                 @('Package_DFN_QFN:QFN-24-1EP_4x4mm_P0.5mm_EP2.6x2.6mm', 90), @('Package_SO:VSSOP-8_3.0x3.0mm_P0.65mm', 180),
                 @('Capacitor_SMD:C_0603_1608Metric', 0), @('EasyEDA:LQFP-64_L10.0-W10.0-P0.50-LS12.0-BL', 0),
                 @('MyParts:SOT-23-5_L3.0-W1.7-P0.95-LS2.8-BR', 0), @('easyeda2kicad:SOT-23', 0), @('', 0))) {
    Eq ("JLC correction '" + $c[0] + "'") ((T 'JlcPlacement').GetMethod('Correction').Invoke($null, @([string]$c[0]))) $c[1]
}
Eq 'BOM footprint 0603' (SCall 'Production' 'BomFootprint' @('C_0603_1608Metric')) '0603'
Eq 'BOM footprint other' (SCall 'Production' 'BomFootprint' @('LQFP-64_L10.0-W10.0-P0.50-LS12.0-BL')) 'LQFP-64_L10.0-W10.0-P0.50-LS12.0-BL'
# through-hole part at 90°: pads at x -1 (round 1 mm) and x 3 (1 x 2 mm) -> local centre (1, 0) -> board (10, 19)
$pcbText = '(kicad_pcb (footprint "Package_TO_SOT_THT:TO-92" (layer "F.Cu") (at 10 20 90) (property "Reference" "Q1" (at 0 0 0)) (attr through_hole) ' +
    '(pad "1" thru_hole circle (at -1 0 90) (size 1 1)) (pad "2" thru_hole rect (at 3 0 90) (size 1 2))) ' +
    '(footprint "Capacitor_SMD:CP_Elec_6.3x7.7" (layer "B.Cu") (at 5 5 180) (property "Reference" "C1" (at 0 0 0)) (attr smd) (pad "1" smd rect (at -2.7 0 180) (size 3.3 2))))'
$board = (T 'JlcPlacement').GetMethod('Read').Invoke($null, @($pcbText))
Eq 'Board footprint THT centre rotated' ("{0},{1}" -f $board['Q1'].CentreX, $board['Q1'].CentreY) '10,19'
Eq 'Board footprint SMD flag' ("{0}/{1}" -f $board['Q1'].Smd, $board['C1'].Smd) 'False/True'
$rows = [Activator]::CreateInstance([Collections.Generic.List``1].MakeGenericType((T 'CplRow')))
$rows.Add((Row 'CplRow' @{ Ref = 'Q1'; X = [double]10; Y = [double]-20; Rotation = [double]90; Side = 'Top' }))
$rows.Add((Row 'CplRow' @{ Ref = 'C1'; X = [double]5; Y = [double]-5; Rotation = (SCall 'Production' 'JlcRotation' @([double]180, $true)); Side = 'Bottom' }))
$polarised = [Collections.Generic.List[string]]::new()
$fixed = (T 'JlcPlacement').GetMethod('Adjust').Invoke($null, @($rows, $board, $polarised))
Eq 'Adjust: THT placed at pad centre' ("{0},{1}" -f $rows[0].X, $rows[0].Y) '10,-19'
Eq 'Adjust: CP_Elec bottom 180 -> 0, not turned' ($rows[1].Rotation) 0
Eq 'Adjust: no rotation correction reported' ($fixed.Count) 0
Eq 'Adjust: polarised capacitor reported for the preview check' ($polarised -join ',') 'C1'
$isPol = (T 'JlcPlacement').GetMethod('IsPolarisedCapacitor')
Eq 'Polarised: KiCad CP_Elec yes, EasyEDA electrolytic no, ceramic no' ("{0}/{1}/{2}" -f $isPol.Invoke($null, @('Capacitor_SMD:CP_Elec_6.3x7.7')),
    $isPol.Invoke($null, @('EasyEDA:CAP-SMD_BD6.3-L6.6-W6.6-LS7.3-FD')), $isPol.Invoke($null, @('Capacitor_SMD:C_0603_1608Metric'))) 'True/False/False'
# ---- LCSC code from the schematic fields ("LCSC Part" first, then "LCSC"; spaces and lower case tolerated)
Eq 'Schematic code LCSC Part' (SCall 'Production' 'SchematicCode' @(, [string[]]@('C14663', 'C99999'))) 'C14663'
Eq 'Schematic code falls back to LCSC' (SCall 'Production' 'SchematicCode' @(, [string[]]@('', ' c21122 '))) 'C21122'
Eq 'Schematic code none' (SCall 'Production' 'SchematicCode' @(, [string[]]@('', 'abc'))) ''

# ---- categories and code validation
Eq 'Category R' (SCall 'Production' 'Category' @('R1,R2')) 'Resistor'
Eq 'Category LED' (SCall 'Production' 'Category' @('LED3')) 'Diode / LED'
Eq 'Category unknown' (SCall 'Production' 'Category' @('ZZ1')) 'Other'
Eq 'Valid code' (SCall 'Production' 'IsValidCode' @('C14663')) $true
Eq 'Invalid code lower case' (SCall 'Production' 'IsValidCode' @('c14663')) $false
Eq 'Invalid code too short' (SCall 'Production' 'IsValidCode' @('C12')) $false

# ---- S-expressions: parentheses and escaped quotes inside strings
$t = '(kicad_pcb (a "x (y)") (b "q \"(\" z") (c (d 1)))'
$root = SCall 'Sexp' 'Root' @($t, 'kicad_pcb')
$kids = SCall 'Sexp' 'Children' @($t, $root.Start, $root.End)
Eq 'Sexp children' (($kids | ForEach-Object Name) -join ',') 'a,b,c'
Eq 'Sexp node text with escaped quote' ($kids[1].Text($t)) '(b "q \"(\" z")'

# ---- .kicad_dru block: user rules kept, legacy Turkish block replaced, a single block remains
$m = [Activator]::CreateInstance((T 'Jlc'))
$sel = $m.Recommended(4, [double]::NaN, [double]::NaN, [double]::NaN)
$rules = $m.Calculate($sel)
$legacy = (T 'Legacy')
$old = "(version 1)`n`n" + $legacy.GetField('DruBlockStart').GetValue($null) + " (JLCPCB, 4 katman)`n(rule ""old"")`n" + $legacy.GetField('DruBlockEnd').GetValue($null) + "`n`n(rule ""mine"" (constraint track_width (min 0.3mm)))`n"
$new = SCall 'ProjectUpdater' 'UpdateDru' @($old, $m, $sel, $rules)
Eq 'Dru legacy block removed' ($new.Contains('(rule "old")')) $false
Eq 'Dru user rule kept' ($new.Contains('(rule "mine"')) $true
Eq 'Dru one app block' ([regex]::Matches($new, 'KiCad DRC Configurator: begin').Count) 1
Eq 'Dru app block before user rule' ($new.IndexOf('KiCad DRC Configurator: end') -lt $new.IndexOf('(rule "mine"')) $true
$again = SCall 'ProjectUpdater' 'UpdateDru' @($new, $m, $sel, $rules)
Eq 'Dru second update still one block' ([regex]::Matches($again, 'KiCad DRC Configurator: begin').Count) 1
Eq 'Dru without version line gets one' ((SCall 'ProjectUpdater' 'UpdateDru' @("(rule ""x"")`n", $m, $sel, $rules)).StartsWith('(version 1)')) $true

# ---- legacy name mapping
Eq 'Legacy mask' (SCall 'Legacy' 'MaskCode' @('Beyaz')) 'white'
Eq 'Legacy edge' (SCall 'Legacy' 'EdgeCode' @('freze')) 'routed'
Eq 'Legacy state key' (SCall 'Legacy' 'StateKey' @('onceki_sablon_dizini')) 'previous_template_dir'
Eq 'Legacy setting key' (SCall 'Legacy' 'SettingKey' @('kart_adedi')) 'board_count'

# ---- JSON writer
$map = [Activator]::CreateInstance((T 'OrderedMap'))
[void]$map.Put('s', "a""b\c`n").Put('d', 0.1).Put('i', 3).Put('whole', 2.0).Put('n', $null)
$json = SCall 'JsonWriter' 'Write' @($map)
Eq 'Json escaping' ($json.Contains('"s": "a\"b\\c\u000a"')) $true
Eq 'Json double' ($json.Contains('"d": 0.1,')) $true
Eq 'Json whole double keeps .0' ($json.Contains('"whole": 2.0,')) $true
Eq 'Json null' ($json.Contains('"n": null')) $true

# ---- English plurals
Eq 'Plural 1 row' ((T 'Tx').GetMethod('RowsNotPlaced').Invoke($null, @(1))) '1 row not placed'
Eq 'Plural 2 rows' ((T 'Tx').GetMethod('RowsNotPlaced').Invoke($null, @(2))) '2 rows not placed'
Eq 'DRC counts singular' ((T 'Tx').GetMethod('DrcCounts').Invoke($null, @(1, 1, 0))) '1 error, 1 warning'

# ---- calculator 1: bandwidth & max conductor length (reference values from Saturn PCB Toolkit, see saturn-compare-bandwidth.ps1)
$bw = T 'BandwidthMath'
function BW([string]$n, [object[]]$a) { ($bw.GetMethods() | Where-Object { $_.Name -eq $n -and $_.GetParameters().Count -eq $a.Count } | Select-Object -First 1).Invoke($null, $a) }
Eq 'Bandwidth 1 ns = 350 MHz' ([math]::Round((BW 'BandwidthFromRise' @(1e-9)) / 1e6, 5)) 350
Eq 'Stripline FR-4 1 ns 0.25 Sr = 1.37578 in' ([math]::Round((BW 'MaxLengthIpc' @(1e-9, 4.6, 0.25)) / 0.0254, 5)) 1.37578
Eq 'Microstrip Saturn coefficient = 1.77221 in' ([math]::Round((BW 'MaxLengthIpc' @(1e-9, (BW 'ErEffective' @(4.6, $true, 0.457)), 0.25)) / 0.0254, 5)) 1.77221
Eq 'Microstrip published coefficient = 1.74632 in' ([math]::Round((BW 'MaxLengthIpc' @(1e-9, (BW 'ErEffective' @(4.6, $true)), 0.25)) / 0.0254, 5)) 1.74632
Eq 'Wavelength 350 MHz = 33.72244 in' ([math]::Round((BW 'WavelengthAir' @(350e6)) / 0.0254, 5)) 33.72244
Eq 'Lambda/7 at 350 MHz = 4.81749 in' ([math]::Round((BW 'MaxLengthFrequency' @(350e6, 7.0)) / 0.0254, 5)) 4.81749
$ui = T 'CalcUi'
Eq 'CalcUi parses comma' ($ui.GetMethod('Parse').Invoke($null, @('1,5'))) 1.5
Eq 'CalcUi length metric' ($ui.GetMethod('Length').Invoke($null, @(0.0450141, $true))) '45.014 mm  (1.7722 in)'
Eq 'CalcUi frequency' ($ui.GetMethod('Frequency').Invoke($null, @(350e6))) '350 MHz'
Eq 'Calculator list has 19 entries' ((T 'CalculatorList').GetMethod('All').Invoke($null, @()).Count) 19

# ---- calculator 1: debug checks (formatting at extremes, figures with extreme values must not throw)
$sig = $ui.GetMethod('Sig')
Eq 'Sig tiny uses scientific' ($sig.Invoke($null, @(1.5e-15, 5))) '1.5E-15'
Eq 'Sig huge uses scientific' ($sig.Invoke($null, @(2.5e18, 5))) '2.5E+18'
Eq 'Sig normal' ($sig.Invoke($null, @(185.3123, 5))) '185.31'
Eq 'Sig NaN' ($sig.Invoke($null, @([double]::NaN, 5))) '—'
Eq 'Time seconds' ($ui.GetMethod('Time').Invoke($null, @(2.0))) '2 s'
Eq 'Time milliseconds' ($ui.GetMethod('Time').Invoke($null, @(0.0025))) '2.5 ms'
Eq 'Time picoseconds' ($ui.GetMethod('Time').Invoke($null, @(5e-11))) '50 ps'
Eq 'Length metres' ($ui.GetMethod('Length').Invoke($null, @(856.55, $true))) '856.55 m  (33722 in)'
$figs = T 'BandwidthFigures'; $stType = T 'BandwidthState'
$bmp = New-Object Drawing.Bitmap 700, 200; $gr = [Drawing.Graphics]::FromImage($bmp)
$extremes = @(
    @{ Sr = 0.1853; LIpc = 0.0463; LFreq = 0.1224; Trace = 0.06; Er = 4.6; ErEff = 2.855 },
    @{ Sr = [double]::NaN; LIpc = [double]::NaN; LFreq = [double]::NaN; Trace = [double]::NaN; Er = [double]::NaN; ErEff = [double]::NaN },
    @{ Sr = 1e-18; LIpc = 1e-19; LFreq = 1e-19; Trace = 1000; Er = 100; ErEff = 100 },
    @{ Sr = 1e12; LIpc = 1e11; LFreq = 3e8; Trace = 0; Er = 1; ErEff = 1 },
    @{ Sr = 0; LIpc = 0; LFreq = 0; Trace = 0.001; Er = 1; ErEff = 1.145 })
$figErrors = 0
foreach ($e in $extremes) {
    $st = [Activator]::CreateInstance($stType)
    foreach ($k in $e.Keys) { $stType.GetField($k).SetValue($st, [double]$e[$k]) }
    foreach ($micro in $true, $false) {
        $stType.GetField('Microstrip').SetValue($st, $micro)
        foreach ($m in 'CrossSection', 'TraceScale') {
            try { $figs.GetMethod($m).Invoke($null, @($gr, ([Drawing.Rectangle]::new(0, 0, 700, 200)), $st)) | Out-Null }
            catch { $figErrors++; $results.Add("FAIL  figure $m threw for " + (($e.Keys | ForEach-Object { "$_=" + $e[$_] }) -join ' ') + ': ' + $_.Exception.InnerException.Message) }
        }
    }
}
foreach ($m in 'EdgeWaveform', 'Wavelength') { foreach ($w in 700, 120, 20) { try { $figs.GetMethod($m).Invoke($null, @($gr, ([Drawing.Rectangle]::new(0, 0, $w, 150)))) | Out-Null } catch { $figErrors++; $results.Add("FAIL  figure $m threw at width $w") } } }
$gr.Dispose(); $bmp.Dispose()
Eq 'Figures draw extreme values without errors' $figErrors 0

# ---- figure labels: free place between markers and chips (a label never crosses them, it is dropped instead)
function Spot([float]$w, [float]$lo, [float]$hi, [object[]]$blocked, [float]$pref) {
    $l = [Collections.Generic.List[float[]]]::new(); foreach ($b in $blocked) { $l.Add([float[]]$b) }
    SCall 'Fig' 'FreeSpot' @($w, $lo, $hi, $l, $pref, [float]4)
}
Eq 'FreeSpot without obstacles keeps the preferred place' (Spot 10 0 100 @() 50) 50
Eq 'FreeSpot moves a label off a marker' (Spot 10 0 100 @(, @(55, 55)) 50) 41
Eq 'FreeSpot skips a chip and a too narrow gap' (Spot 20 0 100 @(, @(70, 90)) 80) 46
Eq 'FreeSpot drops a label that fits nowhere' ([float]::IsNaN((Spot 60 0 100 @(, @(50, 50)) 0))) $true

# ---- calculator 1: reflection model used by the "How it works" demo (physics sanity)
$rm = T 'ReflectionModel'
$os = $rm.GetMethod('OvershootPercent'); $rv = $rm.GetMethod('Receiver')
$td = { param($mm) $mm / 1000 / 177.4e6 }
Eq 'Reflection: settles at 3.3 V' ([math]::Round($rv.Invoke($null, @(200e-9, 1e-9, (& $td 200))), 3)) 3.3
Eq 'Reflection: nothing before the first arrival' ($rv.Invoke($null, @(0.5e-9, 1e-9, (& $td 200)))) 0
Eq 'Reflection: long trace overshoot = open-end limit 54 %' ([math]::Round($os.Invoke($null, @(1e-9, (& $td 300))))) 54
$o20 = $os.Invoke($null, @(1e-9, (& $td 20))); $o44 = $os.Invoke($null, @(1e-9, (& $td 44))); $o120 = $os.Invoke($null, @(1e-9, (& $td 120)))
Eq 'Reflection: overshoot grows with length' ($o20 -lt $o44 -and $o44 -lt $o120) $true

$results
$failed = @($results | Where-Object { $_ -like 'FAIL*' }).Count
"TOTAL: " + ($results.Count - $failed) + " / " + $results.Count + " checks passed"
