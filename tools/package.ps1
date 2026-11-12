param(
    [string]$Version = "0.1.0",
    [string]$OutDir = "dist"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$dist = [System.IO.Path]::GetFullPath($(if ([System.IO.Path]::IsPathRooted($OutDir)) { $OutDir } else { Join-Path $root $OutDir }))
if ($dist -eq [System.IO.Path]::GetFullPath($root)) { throw "OutDir must not be the repository root" }
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

# WinUI ships .mui resources for 80+ locales; only the UI languages we support
# are kept, the rest add hundreds of files to the portable package.
$keepLocales = @("zh-CN", "en-us")
$before = (Get-ChildItem $appDir -Recurse -File).Count
Get-ChildItem $appDir -Directory |
    Where-Object { $_.Name -notin $keepLocales } |
    Where-Object {
        $files = @(Get-ChildItem $_.FullName -Recurse -File)
        $files.Count -gt 0 -and @($files | Where-Object Extension -ne ".mui").Count -eq 0
    } |
    Remove-Item -Recurse -Force
$after = (Get-ChildItem $appDir -Recurse -File).Count
Write-Host "trimmed localization: $before -> $after files"

$appZip = Join-Path $dist "PiggyOcr-v$Version-app.zip"
Write-Host "zipping app ..."
Compress-Archive -Path (Join-Path $appDir "*") -DestinationPath $appZip -CompressionLevel Optimal

# The update zip carries only application files: runtimes and models are
# unchanged between patch releases, so users can overwrite without a 90MB download.
$updateStage = Join-Path $dist "stage-update"
New-Item -ItemType Directory -Force $updateStage | Out-Null
Get-ChildItem $appDir -File |
    Where-Object { $_.Name -like "PiggyOcr.*" -or $_.Extension -eq ".xbf" } |
    Copy-Item -Destination $updateStage
foreach ($name in @("README.md", "LICENSE", "NOTICE")) {
    Copy-Item (Join-Path $appDir $name) (Join-Path $updateStage $name)
}
Copy-Item (Join-Path $appDir "Assets") (Join-Path $updateStage "Assets") -Recurse
$updateZip = Join-Path $dist "PiggyOcr-v$Version-update.zip"
Write-Host "zipping update ..."
Compress-Archive -Path (Join-Path $updateStage "*") -DestinationPath $updateZip -CompressionLevel Optimal
Remove-Item $updateStage -Recurse -Force

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
