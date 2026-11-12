param(
    [string]$App = "D:\AI Workspace\Cherrystudio\Projects\PiggyOcr\src\PiggyOcr.App\bin\x64\Release\net10.0-windows10.0.19041.0\win-x64\PiggyOcr.App.exe",
    [string]$Image = "D:\AI Workspace\Cherrystudio\Projects\PiggyOcr\spikes\test\sample.png",
    [string]$Models = "D:\AI Workspace\Cherrystudio\Projects\PiggyOcr\spikes\models"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class M {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, UIntPtr e);
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  public static void Hotkey() {
    keybd_event(0x5B,0,0,UIntPtr.Zero); keybd_event(0x10,0,0,UIntPtr.Zero); keybd_event(0x4F,0,0,UIntPtr.Zero);
    System.Threading.Thread.Sleep(80);
    keybd_event(0x4F,0,2,UIntPtr.Zero); keybd_event(0x10,0,2,UIntPtr.Zero); keybd_event(0x5B,0,2,UIntPtr.Zero);
  }
  public static void Drag(int x1,int y1,int x2,int y2) {
    SetCursorPos(x1,y1); System.Threading.Thread.Sleep(150);
    mouse_event(2,0,0,0,UIntPtr.Zero); System.Threading.Thread.Sleep(150);
    for (int i=1;i<=20;i++) { SetCursorPos(x1+(x2-x1)*i/20, y1+(y2-y1)*i/20); System.Threading.Thread.Sleep(20); }
    System.Threading.Thread.Sleep(150); mouse_event(4,0,0,0,UIntPtr.Zero);
  }
  public static void Click(int x,int y) {
    SetCursorPos(x,y); System.Threading.Thread.Sleep(150);
    mouse_event(2,0,0,0,UIntPtr.Zero); System.Threading.Thread.Sleep(80); mouse_event(4,0,0,0,UIntPtr.Zero);
  }
}
"@

function Find-ResultWindow([int]$processId) {
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $pidCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $processId)
    $editCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Edit)

    $windows = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $pidCondition)
    foreach ($candidate in $windows) {
        if ($candidate.Current.Name -notlike "*PiggyOcr*") { continue }
        if ($null -ne $candidate.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $editCondition))
            { return $candidate }
    }
    return $null
}

$env:PIGGYOCR_MODELS = $Models

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

$proc = Start-Process -FilePath $App -PassThru
try {
    Start-Sleep -Seconds 9
    [M]::Hotkey()
    Start-Sleep -Milliseconds 900
    [M]::Drag(160, 130, 1340, 510)
    Start-Sleep -Seconds 3

    $result = $null
    for ($i = 0; $i -lt 10 -and $null -eq $result; $i++) {
        $result = Find-ResultWindow $proc.Id
        if ($null -eq $result) { Start-Sleep -Milliseconds 500 }
    }
    if ($null -eq $result) { throw "result window not found" }
    Write-Output ("result window: " + $result.Current.Name)
    [uint32]$fgPidBefore = 0
    [M]::GetWindowThreadProcessId([M]::GetForegroundWindow(), [ref]$fgPidBefore) | Out-Null
    Write-Output ("foreground before: " + [M]::GetForegroundWindow() + " pid: " + $fgPidBefore +
        " app pid: " + $proc.Id + " result hwnd: " + $result.Current.NativeWindowHandle)

    [M]::Click(1800, 1000)
    Start-Sleep -Seconds 2

    $fg = [M]::GetForegroundWindow()
    [uint32]$fgPid = 0
    [M]::GetWindowThreadProcessId($fg, [ref]$fgPid) | Out-Null
    Write-Output ("foreground after: " + $fg + " pid: " + $fgPid)

    $after = Find-ResultWindow $proc.Id
    Write-Output ("closed on outside click: " + ($null -eq $after))
}
finally {
    if (-not $proc.HasExited) { $proc | Stop-Process -Force }
    $form.Close()
    $form.Dispose()
}
