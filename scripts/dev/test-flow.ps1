# Deprecated — use scripts/ci/test.ps1 (Lifecycle layer in BalanceBoard.Automation).
$ErrorActionPreference = "Stop"
Write-Warning "scripts/dev/test-flow.ps1 is deprecated. Running scripts/ci/test.ps1 -Quick instead."
$ci = Join-Path (Split-Path $PSScriptRoot -Parent) "ci\test.ps1"
& $ci -Quick
exit $LASTEXITCODE
