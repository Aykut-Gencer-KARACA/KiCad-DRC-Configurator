# Runs the app's KiCad verification in many scenarios and writes a report into the test results folder.
# Nothing is applied to KiCad (--verify only tests a temporary test board).
#   powershell -ExecutionPolicy Bypass -File run-tests.ps1 [-Exe "other\KiCad DRC.exe"] [-Lang en|tr] [-ReportDir folder]
param([string]$Exe, [string]$Lang = 'en', [string]$ReportDir)
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = New-Object Text.UTF8Encoding $false
$dev = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent   # Development
$exe = if ($Exe) { $Exe } else { Join-Path (Split-Path $dev -Parent) 'KiCad DRC.exe' }
$dir = if ($ReportDir) { $ReportDir } else { Join-Path $dev 'Test Results' }
$report = Join-Path $dir ("verification-report_" + (Get-Date -Format 'yyyy-MM-dd_HHmm') + "_" + $Lang + ".txt")

$scenarios = @()
foreach ($L in 2, 4, 6, 8, 10, 12, 14, 16, 18, 20, 22, 24, 26, 28, 30, 32) { $scenarios += , @('--layers', "$L") }
foreach ($D in '2', '2.5', '3.5', '4.5') { $scenarios += , @('--layers', '2', '--outer', $D) }
$scenarios += , @('--layers', '4', '--outer', '2', '--inner', '2')
$scenarios += , @('--layers', '6', '--outer', '2', '--inner', '1')
$scenarios += , @('--layers', '2', '--margin', '0')
$scenarios += , @('--layers', '6', '--margin', '0.15')
$scenarios += , @('--layers', '4', '--edge', 'routed', '--mask', 'white', '--finish', 'osp')
$scenarios += , @('--layers', '2', '--thickness', '0.4')

$lines = @("KiCad DRC Configurator - test report", ("Date: " + (Get-Date -Format 'yyyy-MM-dd HH:mm') + "   language: " + $Lang),
    ("App: $exe  (v" + (Get-Item $exe).VersionInfo.FileVersion + ")"), "")
$ok = 0
foreach ($s in $scenarios) {
    # Piping waits for the GUI exe; PASS / FAIL / ERROR tokens are the same in every language
    $out = & $exe --lang $Lang @s --verify 2>&1 | ForEach-Object { "$_" }
    $passed = @($out | Where-Object { $_ -match '^\s+PASS' }).Count
    $failed = @($out | Where-Object { $_ -match '^\s+(FAIL|ERROR)' })
    # If no check passed at all (output unreadable) it is not counted as success
    $good = ($LASTEXITCODE -eq 0 -and $failed.Count -eq 0 -and $passed -ge 16)
    if ($good) { $ok++ }
    $lines += ("{0}  {1,-50} {2} checks passed" -f ($(if ($good) { 'OK  ' } else { 'FAIL' }), ($s -join ' '), $passed))
    $lines += $failed | ForEach-Object { "        $_" }
}
$lines += "", "TOTAL: $ok / $($scenarios.Count) scenarios passed"
New-Item -ItemType Directory -Force $dir | Out-Null
[IO.File]::WriteAllLines($report, $lines, (New-Object Text.UTF8Encoding $true))
$lines | Select-Object -Last 1
"Report: $report"
