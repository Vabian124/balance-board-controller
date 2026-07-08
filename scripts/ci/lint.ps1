# Canonical quality gate: format, static analysis, unified test pipeline.
param(
    [switch]$Quick
)

$ErrorActionPreference = "Stop"
$Root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Set-Location $Root

function Resolve-DotNetCli {
    if ($env:DOTNET_ROOT) {
        foreach ($name in @("dotnet.exe", "dotnet")) {
            $candidate = Join-Path $env:DOTNET_ROOT $name
            if (Test-Path $candidate) {
                return $candidate
            }
        }
    }

    return "dotnet"
}

$DotNetCli = Resolve-DotNetCli

Write-Host "=== Balance Board Controller — quality gate ===" -ForegroundColor Cyan

Write-Host "`n=== Stop running app (unlocks build output) ==="
& (Join-Path $Root "scripts\dev\stop.ps1")

Write-Host "`n=== Stop stale test hosts ==="
foreach ($proc in @(Get-Process -Name "testhost", "testhost.net", "BalanceBoardApp" -ErrorAction SilentlyContinue)) {
    try {
        if (-not $proc.HasExited) {
            Stop-Process -Id $proc.Id -Force -ErrorAction Stop
            Write-Host "Stopped $($proc.ProcessName) ($($proc.Id))"
        }
    }
    catch {
        Write-Warning "Failed to stop $($proc.ProcessName) ($($proc.Id)): $($_.Exception.Message)"
    }
}

Write-Host "`n=== Crash-safety grep ==="
& (Join-Path $PSScriptRoot "check-crash-safety.ps1")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`n=== dotnet format ==="
& $DotNetCli format BalanceBoard.sln --verify-no-changes
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`n=== dotnet build (Release, warnings as errors) ==="
& $DotNetCli build BalanceBoard.sln -c Release -warnaserror
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$testMode = if ($Quick) { "quick" } else { "full" }
Write-Host "`n=== Unified test pipeline ($testMode) ==="
$testArgs = @("-SkipBuild")
if ($Quick) { $testArgs += "-Quick" }
& (Join-Path $PSScriptRoot "test.ps1") @testArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`n=== All quality checks passed ===" -ForegroundColor Green
