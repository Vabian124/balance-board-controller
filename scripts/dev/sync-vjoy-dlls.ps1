# Copy vJoyInterface DLLs from a local vJoy install into libs/x64 for a driver/DLL version match.
$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$dest = Join-Path $root "libs\x64"

$searchRoots = @(
    "${env:ProgramFiles}\vJoy\x64",
    "${env:ProgramFiles(x86)}\vJoy\x64",
    "${env:ProgramFiles}\vJoy",
    "${env:ProgramFiles(x86)}\vJoy"
)

$source = $searchRoots | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $source) {
    Write-Error "vJoy install folder not found. Install vJoy, then re-run this script."
}

$files = @("vJoyInterface.dll", "vJoyInterfaceWrap.dll")
foreach ($name in $files) {
    $src = Get-ChildItem -Path $source -Filter $name -Recurse -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if (-not $src) {
        Write-Error "Could not find $name under $source"
    }

    Copy-Item -Path $src.FullName -Destination (Join-Path $dest $name) -Force
    Write-Host "Copied $($src.FullName) -> $(Join-Path $dest $name)"
}

Write-Host "`nDone. Rebuild with: dotnet build BalanceBoard.sln -c Release"
