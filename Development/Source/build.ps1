# Builds "KiCad DRC.exe": the stackup database is embedded gzip-compressed, the icon is generated, every .cs file in this
# folder is compiled with the .NET Framework csc. Default output: "KiCad DRC.exe" two folders up (next to the development folder).
#   powershell -ExecutionPolicy Bypass -File build.ps1 [-Output path\KiCad DRC.exe] [-Data folder-with-stackups.json]
param(
    [string]$Output = (Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'KiCad DRC.exe'),
    [string]$Data = (Join-Path (Split-Path $PSScriptRoot -Parent) 'Reference Data')
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$src = $PSScriptRoot
$obj = Join-Path $src 'obj'; New-Item -ItemType Directory -Force $obj | Out-Null

# stackups.json -> gzip
$raw = [IO.File]::ReadAllBytes((Join-Path $Data 'stackups.json'))
$fs = [IO.File]::Create((Join-Path $obj 'stackups.json.gz'))
$gz = New-Object IO.Compression.GZipStream($fs, [IO.Compression.CompressionMode]::Compress)
$gz.Write($raw, 0, $raw.Length); $gz.Close(); $fs.Close()

# icon: .ico holding a 256 px PNG
$bmp = New-Object Drawing.Bitmap 256, 256
$g = [Drawing.Graphics]::FromImage($bmp); $g.SmoothingMode = 'AntiAlias'; $g.TextRenderingHint = 'AntiAliasGridFit'
$path = New-Object Drawing.Drawing2D.GraphicsPath; $r = 48
$path.AddArc(8, 8, $r, $r, 180, 90); $path.AddArc(248 - $r, 8, $r, $r, 270, 90); $path.AddArc(248 - $r, 248 - $r, $r, $r, 0, 90); $path.AddArc(8, 248 - $r, $r, $r, 90, 90); $path.CloseFigure()
$g.FillPath((New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(24, 32, 46))), $path)
$pen = New-Object Drawing.Pen ([Drawing.Color]::FromArgb(212, 160, 23)), 14
$g.DrawLine($pen, 40, 70, 110, 70); $g.DrawLine($pen, 110, 70, 150, 110); $g.DrawLine($pen, 150, 110, 216, 110)
$g.FillEllipse((New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(212, 160, 23))), 196, 90, 40, 40)
$g.FillEllipse((New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(24, 32, 46))), 206, 100, 20, 20)
$g.DrawString('DRC', (New-Object Drawing.Font('Segoe UI', 64, [Drawing.FontStyle]::Bold, [Drawing.GraphicsUnit]::Pixel)), [Drawing.Brushes]::White, 44, 138)
$g.Dispose()
$ms = New-Object IO.MemoryStream; $bmp.Save($ms, [Drawing.Imaging.ImageFormat]::Png); $png = $ms.ToArray()
$ico = New-Object IO.MemoryStream; $w = New-Object IO.BinaryWriter($ico)
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]1)
$w.Write([byte]0); $w.Write([byte]0); $w.Write([byte]0); $w.Write([byte]0); $w.Write([uint16]1); $w.Write([uint16]32)
$w.Write([uint32]$png.Length); $w.Write([uint32]22); $w.Write($png); $w.Flush()
[IO.File]::WriteAllBytes((Join-Path $obj 'icon.ico'), $ico.ToArray())

$csc = Join-Path ([Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory()) 'csc.exe'
$sources = @(Get-ChildItem $src -Recurse -Filter *.cs | ForEach-Object { $_.FullName })
New-Item -ItemType Directory -Force (Split-Path $Output -Parent) | Out-Null
& $csc /nologo /target:winexe /optimize+ /codepage:65001 "/out:$Output" "/win32icon:$(Join-Path $obj 'icon.ico')" `
    "/resource:$(Join-Path $obj 'stackups.json.gz'),stackups.json.gz" `
    /r:System.Web.Extensions.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll `
    /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll $sources
if ($LASTEXITCODE -ne 0) { throw "Build failed" }
Remove-Item -Recurse -Force $obj
Get-Item $Output | Select-Object FullName, Length
