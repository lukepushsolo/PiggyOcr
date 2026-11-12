param(
    [string]$App = "D:\AI Workspace\Cherrystudio\Projects\PiggyOcr\src\PiggyOcr.App\bin\x64\Release\net10.0-windows10.0.19041.0\win-x64\PiggyOcr.App.exe",
    [string]$Image = "D:\AI Workspace\Cherrystudio\Projects\PiggyOcr\spikes\test\sample.png",
    [string]$Models = "D:\AI Workspace\Cherrystudio\Projects\PiggyOcr\spikes\models",
    [ValidateSet("left", "middle", "right")]
    [string]$Button = "left"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class PiggyInput {
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
  public static void Drag(int x1, int y1, int x2, int y2) {
    SetCursorPos(x1, y1); System.Threading.Thread.Sleep(150);
    mouse_event(LEFTDOWN, 0, 0, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(150);
    for (int i = 1; i <= 24; i++) {
      SetCursorPos(x1 + (x2 - x1) * i / 24, y1 + (y2 - y1) * i / 24);
      System.Threading.Thread.Sleep(20);
    }
    System.Threading.Thread.Sleep(150);
    mouse_event(LEFTUP, 0, 0, 0, UIntPtr.Zero);
  }
  public static void DragMiddle(int x1, int y1, int x2, int y2) {
    SetCursorPos(x1, y1); System.Threading.Thread.Sleep(150);
    mouse_event(0x0020, 0, 0, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(150);
    for (int i = 1; i <= 24; i++) {
      SetCursorPos(x1 + (x2 - x1) * i / 24, y1 + (y2 - y1) * i / 24);
      System.Threading.Thread.Sleep(20);
    }
    System.Threading.Thread.Sleep(150);
    mouse_event(0x0040, 0, 0, 0, UIntPtr.Zero);
  }
  public static void RightClick(int x, int y) {
    SetCursorPos(x, y); System.Threading.Thread.Sleep(150);
    mouse_event(0x0008, 0, 0, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(120);
    mouse_event(0x0010, 0, 0, 0, UIntPtr.Zero);
  }
}
"@

Write-Output "showing reference image"
$form = New-Object System.Windows.Forms.Form
$form.FormBorderStyle = 'None'
$form.StartPosition = 'Manual'
$form.Location = New-Object System.Drawing.Point(150, 120)
$form.Size = New-Object System.Drawing.Size(1200, 400)
$form.TopMost = $false
$picture = New-Object System.Windows.Forms.PictureBox
$picture.Image = [System.Drawing.Image]::FromFile($Image)
$picture.Dock = 'Fill'
$picture.SizeMode = 'StretchImage'
$form.Controls.Add($picture)
$form.Show()
$form.Activate()
[System.Windows.Forms.Application]::DoEvents()

$env:PIGGYOCR_MODELS = $Models
$logPath = Join-Path (Split-Path -Parent $App) "PiggyOcr.log"
$logBefore = if (Test-Path $logPath) { (Get-Content $logPath).Count } else { 0 }

Set-Clipboard -Value "SENTINEL"

$proc = Start-Process -FilePath $App -PassThru
Start-Sleep -Seconds 9

Write-Output "sending hotkey"
[PiggyInput]::Hotkey()
Start-Sleep -Milliseconds 900
[System.Windows.Forms.Application]::DoEvents()

Write-Output "action: $Button"
switch ($Button) {
    "left" { [PiggyInput]::Drag(160, 130, 1340, 510) }
    "middle" { [PiggyInput]::DragMiddle(160, 130, 1340, 510) }
    "right" { [PiggyInput]::RightClick(700, 300) }
}
Start-Sleep -Seconds 3
[System.Windows.Forms.Application]::DoEvents()

$clip = Get-Clipboard -Raw
$logAfter = if (Test-Path $logPath) { (Get-Content $logPath).Count } else { 0 }
$newLines = @()
if ($logAfter -gt $logBefore) { $newLines = (Get-Content $logPath | Select-Object -Skip $logBefore) }

Write-Output "---- clipboard ----"
Write-Output $clip
Write-Output "---- checks ----"
if ($Button -eq "right") {
    Write-Output ("clipboard untouched: " + ($clip -eq "SENTINEL"))
    Write-Output ("no ocr logged: " + (($newLines | Where-Object { $_ -like "*ocr:*" }).Count -eq 0))
} else {
    Write-Output ("has PiggyOcr: " + ($clip -like "*PiggyOcr*"))
    Write-Output ("has DirectML: " + ($clip -like "*DirectML*"))
    Write-Output ("ocr logged: " + (($newLines | Where-Object { $_ -like "*ocr:*" }).Count -gt 0))
}

$proc | Stop-Process -Force
$form.Close()
$form.Dispose()
