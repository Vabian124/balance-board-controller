# Local entry point for the unified test pipeline.
param(
    [switch]$Quick,
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$ciScript = Join-Path (Split-Path $PSScriptRoot -Parent) "ci\test.ps1"
& $ciScript @PSBoundParameters
exit $LASTEXITCODE
