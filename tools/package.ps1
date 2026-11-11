param(
    [string]$Version = "0.1.0"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$dist = Join-Path $root "dist"
$appDir = Join-Path $dist "app"
$modelsSource = Join-Path $root "spikes\models"

Remove-Item $dist -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $appDir | Out-Null

Write-Host "publishing app ..."
& dotnet publish (Join-Path $root "src\PiggyOcr.App\PiggyOcr.App.csproj") `
    -c Release -r win-x64 --self-contained true `
    -p:WindowsAppSDKSelfContained=true -p:WindowsPackageType=None `
    -o $appDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

Copy-Item (Join-Path $root "LICENSE") $appDir
Copy-Item (Join-Path $root "NOTICE") $appDir
Copy-Item (Join-Path $root "README.md") $appDir

$appZip = Join-Path $dist "PiggyOcr-v$Version-app.zip"
Write-Host "zipping app ..."
Compress-Archive -Path (Join-Path $appDir "*") -DestinationPath $appZip -CompressionLevel Optimal

foreach ($t in @("tiny", "small", "medium")) {
    $stage = Join-Path $dist "stage-$t"
    $modelDir = Join-Path $stage "models\$t"
    New-Item -ItemType Directory -Force $modelDir | Out-Null
    Copy-Item (Join-Path $modelsSource "$t\*.onnx") $modelDir
    Copy-Item (Join-Path $root "NOTICE") (Join-Path $stage "NOTICE.txt")

    $zip = Join-Path $dist "PiggyOcr-models-$t.zip"
    Write-Host "zipping $t models ..."
    Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $zip -CompressionLevel Optimal
    Remove-Item $stage -Recurse -Force
}

Write-Host ""
Get-ChildItem $dist -Filter "*.zip" |
    Select-Object Name, @{Name = "SizeMB"; Expression = { [math]::Round($_.Length / 1MB, 1) } } |
    Format-Table -AutoSize
