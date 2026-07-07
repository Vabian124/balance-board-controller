# Canonical quality gate: format, static analysis, tests, tools, lifecycle smoke.
$ErrorActionPreference = "Stop"
$Root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Set-Location $Root

Write-Host "=== Balance Board Controller — quality gate ===" -ForegroundColor Cyan

Write-Host "`n=== Stop running app (unlocks build output) ==="
& (Join-Path $Root "scripts\dev\stop.ps1")

Write-Host "`n=== dotnet format ==="
dotnet format BalanceBoard.sln --verify-no-changes
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`n=== dotnet build (Release, warnings as errors) ==="
dotnet build BalanceBoard.sln -c Release -warnaserror
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$testProjects = @(
    "tests/BalanceBoard.Core.Tests/BalanceBoard.Core.Tests.csproj",
    "tests/BalanceBoard.Integration.Tests/BalanceBoard.Integration.Tests.csproj",
    "tests/BalanceBoard.Fuzz.Tests/BalanceBoard.Fuzz.Tests.csproj",
    "tests/BalanceBoard.Automation/BalanceBoard.Automation.csproj"
)

foreach ($proj in $testProjects) {
    $name = Split-Path (Split-Path $proj -Parent) -Leaf
    Write-Host "`n=== Tests: $name ==="
    dotnet test $proj -c Release --no-build --logger "console;verbosity=minimal"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Host "`n=== Meta: verify test harness ==="
& (Join-Path $PSScriptRoot "verify-tests.ps1") -SkipTestRun
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`n=== Validate (vJoy / HID) ==="
dotnet run --project tools/Validate/BalanceBoard.Validate.csproj -c Release --no-build
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`n=== UI smoke (loads MainWindow XAML) ==="
dotnet run --project tools/UiSmoke/BalanceBoard.UiSmoke.csproj -c Release --no-build
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`n=== Lifecycle smoke ==="
& (Join-Path $Root "scripts\dev\test-flow.ps1")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`n=== All quality checks passed ===" -ForegroundColor Green
