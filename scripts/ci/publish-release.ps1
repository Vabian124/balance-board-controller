# CI / release packaging — framework-dependent win-x64 folder + SHA256 checksum.
$ErrorActionPreference = "Stop"
$Root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Set-Location $Root

$version = (Select-String -Path (Join-Path $Root "Directory.Build.props") -Pattern '<Version>([^<]+)</Version>').Matches[0].Groups[1].Value
$outDir = Join-Path $Root "dist\BalanceBoardController-$version-win-x64"
$zipPath = Join-Path $Root "dist\BalanceBoardController-$version-win-x64.zip"

if (Test-Path $outDir) { Remove-Item $outDir -Recurse -Force }
New-Item -ItemType Directory -Path (Split-Path $outDir) -Force | Out-Null

Write-Host "Publishing $version to $outDir"
dotnet publish src/BalanceBoard.App/BalanceBoard.App.csproj `
    -c Release `
    -r win-x64 `
    --self-contained false `
    -o $outDir
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Copy-Item (Join-Path $Root "LICENSE") $outDir
Copy-Item (Join-Path $Root "THIRD_PARTY_NOTICES.md") $outDir
Copy-Item (Join-Path $Root "docs\INSTALL.md") $outDir

if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path "$outDir\*" -DestinationPath $zipPath

$hash = Get-FileHash $zipPath -Algorithm SHA256
$hashPath = "$zipPath.sha256"
Set-Content -Path $hashPath -Value "$($hash.Hash)  $(Split-Path $zipPath -Leaf)" -NoNewline

Write-Host "ZIP: $zipPath"
Write-Host "SHA256: $($hash.Hash)"
