param(
    [ValidateSet("all", "tiny", "small", "medium")]
    [string]$Tier = "all"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$modelsRoot = Join-Path $root "spikes\models"
$base = "https://www.modelscope.cn/models/RapidAI/RapidOCR/resolve/v3.10.0/onnx/PP-OCRv6"

$manifest = @{
    tiny = @{
        det = "f42c0fbd294d95eac1a550e131b277dac97462c8025fa4b6c3cec1b7894bd3d5"
        rec = "e16e242de5937ad92609223f19bc2aff3727ee40b095f996907c24749bad251b"
    }
    small = @{
        det = "090f04abcd9d9a7498bc4ebf677e4cb9bdce1fe4197ddb7e529f1ef44e1ff94f"
        rec = "6f327246b50388f3c176ae304bd95767ea6dc0c9ae92153ef8cbe210b3c14884"
    }
    medium = @{
        det = "92078b7355007ccfffcd4c8cd441a3afd4538904d06881b29a155e1e679907c2"
        rec = "eef444829dbbe18d7fea59a3f6eb75647518d2b3a9568d27c92e42940204894b"
    }
}

$tiers = if ($Tier -eq "all") { @("tiny", "small", "medium") } else { @($Tier) }

foreach ($t in $tiers) {
    $dir = Join-Path $modelsRoot $t
    New-Item -ItemType Directory -Force $dir | Out-Null

    foreach ($kind in @("det", "rec")) {
        $out = Join-Path $dir "$kind.onnx"
        $expected = $manifest[$t][$kind]

        if (Test-Path $out) {
            $actual = (Get-FileHash $out -Algorithm SHA256).Hash.ToLower()
            if ($actual -eq $expected) {
                Write-Host "$t/$kind ok (cached)"
                continue
            }
            Remove-Item $out -Force
        }

        $url = "$base/$kind/PP-OCRv6_${kind}_$t.onnx"
        Write-Host "downloading $t/$kind ..."
        & curl.exe -L --fail --retry 3 --silent --show-error -o $out $url
        if ($LASTEXITCODE -ne 0) { throw "download failed: $url" }

        $actual = (Get-FileHash $out -Algorithm SHA256).Hash.ToLower()
        if ($actual -ne $expected) { throw "sha256 mismatch for $t/$kind" }
        Write-Host "$t/$kind ok"
    }
}

Write-Host "models ready under $modelsRoot"
