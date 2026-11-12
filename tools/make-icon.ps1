$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$assets = Join-Path $root "src\PiggyOcr.App\Assets"
New-Item -ItemType Directory -Force $assets | Out-Null
$icoPath = Join-Path $assets "PiggyOcr.ico"

$size = 256
$bmp = New-Object System.Drawing.Bitmap $size, $size
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::Transparent)

$rect = New-Object System.Drawing.Rectangle 24, 24, 208, 208
$path = New-Object System.Drawing.Drawing2D.GraphicsPath
$radius = 52
$path.AddArc($rect.X, $rect.Y, $radius, $radius, 180, 90)
$path.AddArc($rect.Right - $radius, $rect.Y, $radius, $radius, 270, 90)
$path.AddArc($rect.Right - $radius, $rect.Bottom - $radius, $radius, $radius, 0, 90)
$path.AddArc($rect.X, $rect.Bottom - $radius, $radius, $radius, 90, 90)
$path.CloseFigure()

$bgBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    (New-Object System.Drawing.Point 24, 24),
    (New-Object System.Drawing.Point 232, 232),
    [System.Drawing.Color]::FromArgb(255, 255, 176, 192),
    [System.Drawing.Color]::FromArgb(255, 240, 108, 138))
$g.FillPath($bgBrush, $path)

$earBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 240, 108, 138))
$g.FillPolygon($earBrush, @(
    (New-Object System.Drawing.Point 64, 78),
    (New-Object System.Drawing.Point 72, 34),
    (New-Object System.Drawing.Point 114, 62)))
$g.FillPolygon($earBrush, @(
    (New-Object System.Drawing.Point 192, 78),
    (New-Object System.Drawing.Point 184, 34),
    (New-Object System.Drawing.Point 142, 62)))

$faceBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 255, 209, 220))
$g.FillEllipse($faceBrush, 46, 58, 164, 154)

$cheekBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(120, 255, 120, 150))
$g.FillEllipse($cheekBrush, 58, 150, 40, 30)
$g.FillEllipse($cheekBrush, 158, 150, 40, 30)

$eyeBrush = [System.Drawing.Brushes]::Black
$g.FillEllipse($eyeBrush, 88, 116, 16, 22)
$g.FillEllipse($eyeBrush, 152, 116, 16, 22)

$snoutBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 240, 108, 138))
$g.FillEllipse($snoutBrush, 96, 148, 64, 44)
$nostrilBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 150, 40, 70))
$g.FillEllipse($nostrilBrush, 112, 162, 12, 16)
$g.FillEllipse($nostrilBrush, 132, 162, 12, 16)

$g.Dispose()

$stream = New-Object System.IO.MemoryStream
$bmp.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
$png = $stream.ToArray()
$stream.Dispose()
$bmp.Dispose()

$file = [System.IO.File]::Create($icoPath)
$writer = New-Object System.IO.BinaryWriter $file
$writer.Write([UInt16]0)
$writer.Write([UInt16]1)
$writer.Write([UInt16]1)
$writer.Write([Byte]0)
$writer.Write([Byte]0)
$writer.Write([Byte]0)
$writer.Write([Byte]0)
$writer.Write([UInt16]1)
$writer.Write([UInt16]32)
$writer.Write([UInt32]$png.Length)
$writer.Write([UInt32]22)
$writer.Write($png)
$writer.Close()

Write-Host "wrote $icoPath ($($png.Length) bytes png)"
