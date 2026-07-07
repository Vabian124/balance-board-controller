# Meta-test: verify the test harness itself runs and reports failures correctly.
$ErrorActionPreference = "Stop"
$Root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Set-Location $Root

Write-Host "=== verify-tests: build ==="
dotnet build BalanceBoard.sln -c Release -warnaserror
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$projects = @(
    "tests/BalanceBoard.Core.Tests/BalanceBoard.Core.Tests.csproj",
    "tests/BalanceBoard.Integration.Tests/BalanceBoard.Integration.Tests.csproj",
    "tests/BalanceBoard.Fuzz.Tests/BalanceBoard.Fuzz.Tests.csproj"
)

foreach ($proj in $projects) {
    Write-Host "`n=== verify-tests: $proj ==="
    dotnet test $proj -c Release --no-build --logger "console;verbosity=minimal"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Host "`n=== verify-tests: automation (simulate board) ==="
dotnet test tests/BalanceBoard.Automation/BalanceBoard.Automation.csproj -c Release --no-build --logger "console;verbosity=minimal"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`n=== verify-tests passed ==="
