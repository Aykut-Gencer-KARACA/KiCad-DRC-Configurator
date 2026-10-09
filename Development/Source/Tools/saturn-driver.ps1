# Remote control for Saturn PCB Toolkit (black-box reference for the calculator tabs). Saturn itself is not part of this project;
# download it from https://www.saturnpcb.com. Dot-source this file:   . .\saturn-driver.ps1
#   Start-Saturn [-Path exe] [-X 2580 -Y 20]    starts (or finds) Saturn and places its window
#   Get-SaturnControls                          windowed controls: handle, class, text, position relative to the window
#   Select-SaturnTab "Via Properties"           switches the tab by its caption
#   Set-SaturnText $h "1.5"; Get-SaturnText $h; Invoke-SaturnClick $h; Set-SaturnCombo $h 2; Set-SaturnTrack $h 3
#   Save-SaturnImage file.png                   picture of the window (it must be on a screen)
param()
Add-Type -AssemblyName System.Drawing
if (-not ('SaturnWin' -as [type])) {
Add-Type -ReferencedAssemblies System.Drawing @'
using System; using System.Collections.Generic; using System.Runtime.InteropServices; using System.Text;
public static class SaturnWin {
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc f, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr p, EnumProc f, IntPtr l);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr SendMessage(IntPtr h, int m, IntPtr w, StringBuilder l);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr SendMessage(IntPtr h, int m, IntPtr w, string l);
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, int m, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, int m, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] public static extern IntPtr GetParent(IntPtr h);
    [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr h, int x, int y, int w, int hh, bool r);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr dc, uint f);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint a, bool i, uint pid);
    [DllImport("kernel32.dll")] static extern IntPtr VirtualAllocEx(IntPtr p, IntPtr a, int s, uint t, uint pr);
    [DllImport("kernel32.dll")] static extern bool VirtualFreeEx(IntPtr p, IntPtr a, int s, uint t);
    [DllImport("kernel32.dll")] static extern bool ReadProcessMemory(IntPtr p, IntPtr a, byte[] b, int s, out IntPtr r);
    [DllImport("kernel32.dll")] static extern bool WriteProcessMemory(IntPtr p, IntPtr a, byte[] b, int s, out IntPtr r);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);

    public static string Text(IntPtr h) { var sb = new StringBuilder(1024); SendMessage(h, 0x000D, (IntPtr)1024, sb); return sb.ToString(); }
    public static string Class(IntPtr h) { var sb = new StringBuilder(256); GetClassName(h, sb, 256); return sb.ToString(); }
    public static List<IntPtr> TopWindows(uint pid) { var r = new List<IntPtr>(); EnumWindows((h, l) => { uint p; GetWindowThreadProcessId(h, out p); if (p == pid) r.Add(h); return true; }, IntPtr.Zero); return r; }
    public static List<IntPtr> Children(IntPtr parent) { var r = new List<IntPtr>(); EnumChildWindows(parent, (h, l) => { r.Add(h); return true; }, IntPtr.Zero); return r; }

    // Tab control item rectangles live in Saturn's memory (TCM_GETITEMRECT is not marshalled between processes)
    public static int[] TabRect(IntPtr tab, int index) {
        uint pid; GetWindowThreadProcessId(tab, out pid);
        IntPtr proc = OpenProcess(0x0008 | 0x0010 | 0x0020 | 0x0400, false, pid);
        IntPtr mem = VirtualAllocEx(proc, IntPtr.Zero, 16, 0x3000, 0x04);
        try {
            SendMessage(tab, 0x130A, (IntPtr)index, mem);
            var b = new byte[16]; IntPtr n; ReadProcessMemory(proc, mem, b, 16, out n);
            return new[] { BitConverter.ToInt32(b, 0), BitConverter.ToInt32(b, 4), BitConverter.ToInt32(b, 8), BitConverter.ToInt32(b, 12) };
        } finally { VirtualFreeEx(proc, mem, 0, 0x8000); CloseHandle(proc); }
    }
    [DllImport("kernel32.dll")] static extern bool IsWow64Process(IntPtr p, out bool wow);
    [DllImport("user32.dll")] static extern IntPtr SendMessageTimeout(IntPtr h, int m, IntPtr w, IntPtr l, int flags, int timeout, out IntPtr result);

    // Tab caption: TCM_GETITEMW with a TCITEM and text buffer allocated in Saturn's memory. The TCITEM layout depends on
    // the bitness of the TARGET process (Saturn is 32-bit).
    public static string TabText(IntPtr tab, int index) {
        uint pid; GetWindowThreadProcessId(tab, out pid);
        IntPtr proc = OpenProcess(0x0008 | 0x0010 | 0x0020 | 0x0400, false, pid);
        IntPtr mem = VirtualAllocEx(proc, IntPtr.Zero, 4096, 0x3000, 0x04);
        try {
            bool wow; IsWow64Process(proc, out wow);
            bool target64 = Environment.Is64BitOperatingSystem && !wow;
            var item = new byte[64];
            BitConverter.GetBytes(0x0001).CopyTo(item, 0);                                   // mask = TCIF_TEXT
            long textAddr = mem.ToInt64() + 512;
            if (target64) { BitConverter.GetBytes(textAddr).CopyTo(item, 16); BitConverter.GetBytes(256).CopyTo(item, 24); }
            else { BitConverter.GetBytes((int)textAddr).CopyTo(item, 12); BitConverter.GetBytes(256).CopyTo(item, 16); }
            IntPtr n; WriteProcessMemory(proc, mem, item, item.Length, out n);
            IntPtr res; SendMessageTimeout(tab, 0x133C, (IntPtr)index, mem, 0x0002, 3000, out res);   // SMTO_ABORTIFHUNG
            var b = new byte[512]; ReadProcessMemory(proc, (IntPtr)textAddr, b, 512, out n);
            string s = Encoding.Unicode.GetString(b); int z = s.IndexOf('\0'); return z >= 0 ? s.Substring(0, z) : s;
        } finally { VirtualFreeEx(proc, mem, 0, 0x8000); CloseHandle(proc); }
    }
}
'@
}

$script:SaturnPath = 'C:\Downloaded Apps\SaturnPCB\PCB Toolkit V8.47.exe'
$script:Saturn = $null   # main window handle

function Start-Saturn([string]$Path = $script:SaturnPath, [int]$X = 2580, [int]$Y = 20) {
    $p = Get-Process | Where-Object { $_.Path -eq $Path } | Select-Object -First 1
    if (-not $p) { $p = Start-Process $Path -PassThru }
    $sw = [Diagnostics.Stopwatch]::StartNew()
    do {
        Start-Sleep -Milliseconds 300
        $main = [SaturnWin]::TopWindows([uint32]$p.Id) | Where-Object { [SaturnWin]::Class($_) -eq 'TForm1' -and [SaturnWin]::IsWindowVisible($_) } | Select-Object -First 1
    } while (-not $main -and $sw.ElapsedMilliseconds -lt 30000)
    if (-not $main) { throw 'Saturn main window not found' }
    $script:Saturn = $main
    [SaturnWin]::MoveWindow($main, $X, $Y, 985, 830, $true) | Out-Null
    Start-Sleep -Milliseconds 500
    $p.Id
}

function Stop-Saturn { Get-Process | Where-Object { $_.Path -eq $script:SaturnPath } | Stop-Process }

# Windowed controls with position relative to the main window (Delphi labels are not windows and are not listed)
function Get-SaturnControls([switch]$VisibleOnly) {
    $w = New-Object SaturnWin+RECT; [SaturnWin]::GetWindowRect($script:Saturn, [ref]$w) | Out-Null
    foreach ($h in [SaturnWin]::Children($script:Saturn)) {
        $vis = [SaturnWin]::IsWindowVisible($h)
        if ($VisibleOnly -and -not $vis) { continue }
        $r = New-Object SaturnWin+RECT; [SaturnWin]::GetWindowRect($h, [ref]$r) | Out-Null
        [pscustomobject]@{ Handle = [int64]$h; Class = [SaturnWin]::Class($h); Text = [SaturnWin]::Text($h); X = $r.L - $w.L; Y = $r.T - $w.T; W = $r.R - $r.L; H = $r.B - $r.T; Visible = $vis }
    }
}

function Select-SaturnTab([string]$Caption) {
    $tab = [SaturnWin]::Children($script:Saturn) | Where-Object { [SaturnWin]::Class($_) -eq 'TTabControl' } | Select-Object -First 1
    $count = [int][SaturnWin]::SendMessage($tab, 0x1304, [IntPtr]::Zero, [IntPtr]::Zero)   # TCM_GETITEMCOUNT
    for ($i = 0; $i -lt $count; $i++) {
        if ([SaturnWin]::TabText($tab, $i) -ne $Caption) { continue }
        $r = [SaturnWin]::TabRect($tab, $i)
        $lp = [IntPtr](((($r[1] + $r[3]) / 2) -shl 16) -bor (($r[0] + $r[2]) / 2))
        [SaturnWin]::PostMessage($tab, 0x0201, [IntPtr]1, $lp) | Out-Null   # WM_LBUTTONDOWN
        [SaturnWin]::PostMessage($tab, 0x0202, [IntPtr]0, $lp) | Out-Null   # WM_LBUTTONUP
        Start-Sleep -Milliseconds 600
        return $true
    }
    throw "Tab not found: $Caption"
}

function Get-SaturnTabs {
    $tab = [SaturnWin]::Children($script:Saturn) | Where-Object { [SaturnWin]::Class($_) -eq 'TTabControl' } | Select-Object -First 1
    $count = [int][SaturnWin]::SendMessage($tab, 0x1304, [IntPtr]::Zero, [IntPtr]::Zero)
    0..($count - 1) | ForEach-Object { [SaturnWin]::TabText($tab, $_) }
}

function Get-SaturnText([int64]$h) { [SaturnWin]::Text([IntPtr]$h) }

# WM_SETTEXT raises EN_CHANGE, so Saturn's OnChange handlers recalculate
function Set-SaturnText([int64]$h, [string]$value) { [SaturnWin]::SendMessage([IntPtr]$h, 0x000C, [IntPtr]::Zero, $value) | Out-Null; Start-Sleep -Milliseconds 150 }

function Invoke-SaturnClick([int64]$h) { [SaturnWin]::SendMessage([IntPtr]$h, 0x00F5, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null; Start-Sleep -Milliseconds 250 }   # BM_CLICK

function Get-SaturnComboItems([int64]$h) {
    $n = [int][SaturnWin]::SendMessage([IntPtr]$h, 0x0146, [IntPtr]::Zero, [IntPtr]::Zero)   # CB_GETCOUNT
    0..($n - 1) | ForEach-Object { $sb = New-Object Text.StringBuilder 512; [SaturnWin]::SendMessage([IntPtr]$h, 0x0148, [IntPtr]$_, $sb) | Out-Null; $sb.ToString() }
}

function Set-SaturnCombo([int64]$h, [int]$index) {
    [SaturnWin]::SendMessage([IntPtr]$h, 0x014E, [IntPtr]$index, [IntPtr]::Zero) | Out-Null   # CB_SETCURSEL
    $id = 0; $parent = [SaturnWin]::GetParent([IntPtr]$h)
    # WM_COMMAND with CBN_SELCHANGE (1) in the high word; the control handle identifies the sender for the VCL
    [SaturnWin]::SendMessage($parent, 0x0111, [IntPtr](1 -shl 16), [IntPtr]$h) | Out-Null
    Start-Sleep -Milliseconds 250
}

function Set-SaturnTrack([int64]$h, [int]$pos) {
    [SaturnWin]::SendMessage([IntPtr]$h, 0x0405, [IntPtr]1, [IntPtr]$pos) | Out-Null         # TBM_SETPOS
    $parent = [SaturnWin]::GetParent([IntPtr]$h)
    [SaturnWin]::SendMessage($parent, 0x0114, [IntPtr](4 -bor ($pos -shl 16)), [IntPtr]$h) | Out-Null   # WM_HSCROLL, SB_THUMBPOSITION
    [SaturnWin]::SendMessage($parent, 0x0114, [IntPtr]8, [IntPtr]$h) | Out-Null                        # SB_ENDSCROLL
    Start-Sleep -Milliseconds 250
}

function Get-SaturnTrack([int64]$h) {
    [pscustomobject]@{ Pos = [int][SaturnWin]::SendMessage([IntPtr]$h, 0x0400, [IntPtr]::Zero, [IntPtr]::Zero)
        Min = [int][SaturnWin]::SendMessage([IntPtr]$h, 0x0401, [IntPtr]::Zero, [IntPtr]::Zero)
        Max = [int][SaturnWin]::SendMessage([IntPtr]$h, 0x0402, [IntPtr]::Zero, [IntPtr]::Zero) }
}

function Save-SaturnImage([string]$file) {
    $r = New-Object SaturnWin+RECT; [SaturnWin]::GetWindowRect($script:Saturn, [ref]$r) | Out-Null
    $bmp = New-Object Drawing.Bitmap ($r.R - $r.L), ($r.B - $r.T)
    $g = [Drawing.Graphics]::FromImage($bmp); $dc = $g.GetHdc(); [SaturnWin]::PrintWindow($script:Saturn, $dc, 2) | Out-Null; $g.ReleaseHdc($dc); $g.Dispose()
    $bmp.Save($file); $bmp.Dispose()
}
