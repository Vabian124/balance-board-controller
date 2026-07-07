# Lint + static analysis (run before commit or in CI)
$ErrorActionPreference = "Stop"
$Root = Split-Path $PSScriptRoot -Parent
Set-Location $Root

Write-Host "=== Stop running app (unlocks build output) ==="
& (Join-Path $PSScriptRoot "stop.ps1")

Write-Host "`n=== dotnet format ==="
dotnet format BalanceBoard.sln --verify-no-changes
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`n=== dotnet build (warnings as errors) ==="
dotnet build BalanceBoard.sln -c Release -warnaserror
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`n=== Unit tests (portable core contract) ==="
dotnet test tests/BalanceBoard.Core.Tests/BalanceBoard.Core.Tests.csproj -c Release --no-build
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`n=== Integration tests (connect flows) ==="
dotnet test tests/BalanceBoard.Integration.Tests/BalanceBoard.Integration.Tests.csproj -c Release --no-build
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`n=== Fuzz tests ==="
dotnet test tests/BalanceBoard.Fuzz.Tests/BalanceBoard.Fuzz.Tests.csproj -c Release --no-build
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`n=== Automation (simulate board process) ==="
dotnet test tests/BalanceBoard.Automation/BalanceBoard.Automation.csproj -c Release --no-build
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`n=== verify-tests harness ==="
& (Join-Path $PSScriptRoot "ci\verify-tests.ps1")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`n=== Validate (vJoy / HID) ==="
dotnet run --project tools/Validate/BalanceBoard.Validate.csproj -c Release --no-build
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`n=== UI smoke (loads MainWindow XAML) ==="
dotnet run --project tools/UiSmoke/BalanceBoard.UiSmoke.csproj -c Release --no-build
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`n=== Lifecycle smoke ==="
& (Join-Path $PSScriptRoot "test-flow.ps1")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`n=== All lint checks passed ==="
