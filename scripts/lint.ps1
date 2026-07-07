# Entry point — delegates to scripts/ci/lint.ps1
$ErrorActionPreference = "Stop"
& (Join-Path $PSScriptRoot "ci\lint.ps1")
exit $LASTEXITCODE
