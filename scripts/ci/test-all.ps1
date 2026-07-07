param(
    [switch]$IncludeHardware
)

$ErrorActionPreference = "Stop"
$Root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Set-Location $Root

& (Join-Path $PSScriptRoot "lint.ps1")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if ($IncludeHardware) {
    $hardware = Join-Path $Root "tests\hardware\Run-HardwareConnect.ps1"
    if (-not (Test-Path $hardware)) {
        Write-Error "Hardware script missing: $hardware"
    }
    & $hardware
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Host "`n=== test-all passed ===" -ForegroundColor Green
