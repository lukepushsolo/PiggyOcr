param(
    [string]$App = "D:\AI Workspace\Cherrystudio\Projects\PiggyOcr\src\PiggyOcr.App\bin\x64\Release\net10.0-windows10.0.19041.0\win-x64\PiggyOcr.App.exe"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Keys {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
  public static void CtrlShiftK() {
    keybd_event(0x11, 0, 0, UIntPtr.Zero);
    keybd_event(0x10, 0, 0, UIntPtr.Zero);
    keybd_event(0x4B, 0, 0, UIntPtr.Zero);
    System.Threading.Thread.Sleep(100);
    keybd_event(0x4B, 0, 2, UIntPtr.Zero);
    keybd_event(0x10, 0, 2, UIntPtr.Zero);
    keybd_event(0x11, 0, 2, UIntPtr.Zero);
  }
}
"@

$appDir = Split-Path -Parent $App
$configPath = Join-Path $appDir "PiggyOcr.json"
$backup = $null
if (Test-Path $configPath) {
    $backup = Get-Content $configPath -Raw
    Remove-Item $configPath -Force
}

$proc = Start-Process -FilePath $App -ArgumentList "--settings" -PassThru

try {
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $pidCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $proc.Id)

    $editCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Edit)

    $window = $null
    $edit = $null
    for ($i = 0; $i -lt 20 -and $null -eq $window; $i++) {
        Start-Sleep -Milliseconds 500
        $windows = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $pidCondition)
        foreach ($candidate in $windows) {
            if ($candidate.Current.Name -notlike "*PiggyOcr*") { continue }
            $candidateEdit = $candidate.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $editCondition)
            if ($null -ne $candidateEdit) {
                $window = $candidate
                $edit = $candidateEdit
                break
            }
        }
    }

    if ($null -eq $window) {
        $windows = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $pidCondition)
        foreach ($candidate in $windows) {
            Write-Output ("window '" + $candidate.Current.Name + "'")
            $all = $candidate.FindAll([System.Windows.Automation.TreeScope]::Descendants,
                [System.Windows.Automation.Condition]::TrueCondition)
            for ($j = 0; $j -lt [Math]::Min(20, $all.Count); $j++) {
                Write-Output ("  " + $all[$j].Current.ControlType.ProgrammaticName + " '" + $all[$j].Current.Name + "'")
            }
        }
        throw "settings window with an edit control not found"
    }

    [Keys]::SetForegroundWindow([IntPtr]$window.Current.NativeWindowHandle) | Out-Null
    $edit.SetFocus()
    Start-Sleep -Milliseconds 400
    [Keys]::CtrlShiftK()
    Start-Sleep -Seconds 2

    if (-not (Test-Path $configPath)) { throw "config was not written" }
    $config = Get-Content $configPath -Raw | ConvertFrom-Json
    Write-Output ("recorded hotkey: " + $config.Hotkey)
    Write-Output ("pass: " + ($config.Hotkey -eq "Ctrl+Shift+K"))
}
finally {
    if (-not $proc.HasExited) { $proc | Stop-Process -Force }
    if ($null -ne $backup) {
        Set-Content -Path $configPath -Value $backup -NoNewline
    } elseif (Test-Path $configPath) {
        Remove-Item $configPath -Force
    }
}
