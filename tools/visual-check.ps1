param(
    [string]$App = "D:\AI Workspace\Cherrystudio\Projects\PiggyOcr\src\PiggyOcr.App\bin\x64\Release\net10.0-windows10.0.19041.0\win-x64\PiggyOcr.App.exe",
    [string]$Image = "D:\AI Workspace\Cherrystudio\Projects\PiggyOcr\spikes\test\sample.png",
    [string]$Models = "D:\AI Workspace\Cherrystudio\Projects\PiggyOcr\spikes\models",
    [string]$OutDir = "D:\AI Workspace\Cherrystudio\Projects\PiggyOcr\dist"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

Add-Type -ReferencedAssemblies System.Drawing, System.Windows.Forms -TypeDefinition @"
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public static class QaInput {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, UIntPtr e);
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
  public const uint LEFTDOWN = 0x0002, LEFTUP = 0x0004;
  public static void Hotkey() {
    keybd_event(0x5B, 0, 0, UIntPtr.Zero);
    keybd_event(0x10, 0, 0, UIntPtr.Zero);
    keybd_event(0x4F, 0, 0, UIntPtr.Zero);
    System.Threading.Thread.Sleep(80);
    keybd_event(0x4F, 0, 2, UIntPtr.Zero);
    keybd_event(0x10, 0, 2, UIntPtr.Zero);
    keybd_event(0x5B, 0, 2, UIntPtr.Zero);
  }
  public static void Escape() {
    keybd_event(0x1B, 0, 0, UIntPtr.Zero);
    System.Threading.Thread.Sleep(60);
    keybd_event(0x1B, 0, 2, UIntPtr.Zero);
  }
  public static void Drag(int x1, int y1, int x2, int y2) {
    SetCursorPos(x1, y1); System.Threading.Thread.Sleep(120);
    mouse_event(LEFTDOWN, 0, 0, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(120);
    for (int i = 1; i <= 20; i++) {
      SetCursorPos(x1 + (x2 - x1) * i / 20, y1 + (y2 - y1) * i / 20);
      System.Threading.Thread.Sleep(20);
    }
    System.Threading.Thread.Sleep(120);
    mouse_event(LEFTUP, 0, 0, 0, UIntPtr.Zero);
  }
}

public static class QaCapture {
  [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hWnd);
  [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
  [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr ctx);
  [DllImport("user32.dll")] static extern int GetSystemMetrics(int index);
  [DllImport("gdi32.dll")] static extern bool BitBlt(IntPtr dst, int x, int y, int w, int h, IntPtr src, int sx, int sy, uint rop);
  [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc cb, IntPtr l);
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, System.Text.StringBuilder s, int n);
  [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
  delegate bool EnumWindowsProc(IntPtr h, IntPtr l);
  [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }

  public static void DumpWindows(int pid) {
    IntPtr old = SetThreadDpiAwarenessContext(new IntPtr(-4));
    try {
      EnumWindows(delegate(IntPtr h, IntPtr l) {
        uint owner; GetWindowThreadProcessId(h, out owner);
        if (owner == (uint)pid && IsWindowVisible(h)) {
          var sb = new System.Text.StringBuilder(256);
          GetWindowText(h, sb, sb.Capacity);
          RECT r; GetWindowRect(h, out r);
          Console.WriteLine("win '" + sb + "' rect=" + r.Left + "," + r.Top + " " + (r.Right - r.Left) + "x" + (r.Bottom - r.Top));
        }
        return true;
      }, IntPtr.Zero);
    } finally {
      SetThreadDpiAwarenessContext(old);
    }
  }

  public static void Save(string path) {
    IntPtr old = SetThreadDpiAwarenessContext(new IntPtr(-4));
    try {
      int x = GetSystemMetrics(76), y = GetSystemMetrics(77);
      int w = GetSystemMetrics(78), h = GetSystemMetrics(79);
      using (Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb)) {
        using (Graphics g = Graphics.FromImage(bmp)) {
          IntPtr dst = g.GetHdc();
          IntPtr src = GetDC(IntPtr.Zero);
          BitBlt(dst, 0, 0, w, h, src, x, y, 0x40CC0020);
          ReleaseDC(IntPtr.Zero, src);
          g.ReleaseHdc(dst);
        }
        bmp.Save(path, ImageFormat.Png);
      }
    } finally {
      SetThreadDpiAwarenessContext(old);
    }
  }
}
"@

function Save-Screen([string]$path) {
    [QaCapture]::Save($path)
    Write-Host "saved $path"
}

$env:PIGGYOCR_MODELS = $Models

Write-Host "capture settings"
$p = Start-Process -FilePath $App -ArgumentList "--settings" -PassThru
Start-Sleep -Seconds 5
Save-Screen (Join-Path $OutDir "qa-settings.png")
$p | Stop-Process -Force
Start-Sleep -Seconds 2

Write-Host "capture overlay"
$p = Start-Process -FilePath $App -PassThru
Start-Sleep -Seconds 9
[QaInput]::Hotkey()
Start-Sleep -Milliseconds 900
Save-Screen (Join-Path $OutDir "qa-overlay.png")
[QaInput]::Escape()
Start-Sleep -Seconds 2

Write-Host "capture result"
$form = New-Object System.Windows.Forms.Form
$form.FormBorderStyle = 'None'
$form.StartPosition = 'Manual'
$form.Location = New-Object System.Drawing.Point(150, 120)
$form.Size = New-Object System.Drawing.Size(1200, 400)
$form.TopMost = $true
$picture = New-Object System.Windows.Forms.PictureBox
$picture.Image = [System.Drawing.Image]::FromFile($Image)
$picture.Dock = 'Fill'
$picture.SizeMode = 'StretchImage'
$form.Controls.Add($picture)
$form.Show()
Start-Sleep -Seconds 1

[QaInput]::Hotkey()
Start-Sleep -Milliseconds 900
[QaInput]::Drag(160, 130, 1340, 510)
$form.TopMost = $false
Start-Sleep -Seconds 2
$form.Location = New-Object System.Drawing.Point(-3000, -3000)
Start-Sleep -Milliseconds 500
Save-Screen (Join-Path $OutDir "qa-result.png")
[QaCapture]::DumpWindows($p.Id)

$p | Stop-Process -Force
$form.Close()
$form.Dispose()
