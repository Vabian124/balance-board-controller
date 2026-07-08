# Canonical quality gate: format, static analysis, unified test pipeline.
$ErrorActionPreference = "Stop"
$Root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Set-Location $Root

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
dotnet format BalanceBoard.sln --verify-no-changes
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`n=== dotnet build (Release, warnings as errors) ==="
dotnet build BalanceBoard.sln -c Release -warnaserror
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`n=== Unified test pipeline (full) ==="
& (Join-Path $PSScriptRoot "test.ps1") -SkipBuild
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`n=== All quality checks passed ===" -ForegroundColor Green
