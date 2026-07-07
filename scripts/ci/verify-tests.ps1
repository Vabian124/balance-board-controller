# Meta-test: verify the test pyramid is wired correctly (project discovery + minimum counts).
param(
    [switch]$SkipTestRun
)

$ErrorActionPreference = "Stop"
$Root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Set-Location $Root

$expectedProjects = @(
    @{ Path = "tests/BalanceBoard.Core.Tests/BalanceBoard.Core.Tests.csproj"; MinTests = 20 },
    @{ Path = "tests/BalanceBoard.Integration.Tests/BalanceBoard.Integration.Tests.csproj"; MinTests = 10 },
    @{ Path = "tests/BalanceBoard.Fuzz.Tests/BalanceBoard.Fuzz.Tests.csproj"; MinTests = 3 },
    @{ Path = "tests/BalanceBoard.Automation/BalanceBoard.Automation.csproj"; MinTests = 1 }
)

foreach ($entry in $expectedProjects) {
    if (-not (Test-Path $entry.Path)) {
        Write-Error "Missing test project: $($entry.Path)"
    }
}

$solution = Get-Content "BalanceBoard.sln" -Raw
foreach ($entry in $expectedProjects) {
    $folder = Split-Path (Split-Path $entry.Path -Parent) -Leaf
    if ($solution -notmatch [regex]::Escape($folder)) {
        Write-Error "Test project not listed in BalanceBoard.sln: $folder"
    }
}

if ($SkipTestRun) {
    Write-Host "verify-tests: solution wiring OK ($($expectedProjects.Count) projects)."
    exit 0
}

dotnet build BalanceBoard.sln -c Release -warnaserror
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

foreach ($entry in $expectedProjects) {
    $listed = dotnet test $entry.Path -c Release --no-build --list-tests 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Test discovery failed: $($entry.Path)"
    }

    $count = @($listed | Where-Object { $_ -match '^\s+' }).Count
    if ($count -lt $entry.MinTests) {
        Write-Error "Expected >= $($entry.MinTests) tests in $($entry.Path), found $count"
    }

    Write-Host "verify-tests: $($entry.Path) -> $count tests"
}

Write-Host "verify-tests passed."
