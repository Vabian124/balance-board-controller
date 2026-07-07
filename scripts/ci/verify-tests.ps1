# Meta-test: verify the test pyramid is wired correctly (project discovery + minimum counts).
param(
    [switch]$SkipTestRun,
    [switch]$VerifyCountsOnly
)

$ErrorActionPreference = "Stop"
$Root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Set-Location $Root

$expectedProjects = @(
    @{ Path = "tests/BalanceBoard.Core.Tests/BalanceBoard.Core.Tests.csproj"; MinTests = 40 },
    @{ Path = "tests/BalanceBoard.Integration.Tests/BalanceBoard.Integration.Tests.csproj"; MinTests = 10 },
    @{ Path = "tests/BalanceBoard.Fuzz.Tests/BalanceBoard.Fuzz.Tests.csproj"; MinTests = 4 },
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

function Test-ProjectCounts {
    param([switch]$NoBuild)

    foreach ($entry in $expectedProjects) {
        $listArgs = @("test", $entry.Path, "-c", "Release", "--list-tests")
        if ($NoBuild) {
            $listArgs += "--no-build"
        }

        $listed = & dotnet @listArgs 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Test discovery failed: $($entry.Path)`n$listed"
        }

        $count = @($listed | Where-Object { $_ -match '^\s{2,}\S' }).Count
        if ($count -lt $entry.MinTests) {
            Write-Error "Expected >= $($entry.MinTests) tests in $($entry.Path), found $count"
        }

        Write-Host "verify-tests: $($entry.Path) -> $count tests"
    }
}

if ($SkipTestRun) {
    Write-Host "verify-tests: solution wiring OK ($($expectedProjects.Count) projects)."
    exit 0
}

if ($VerifyCountsOnly) {
    Test-ProjectCounts -NoBuild
    Write-Host "verify-tests: counts OK."
    exit 0
}

dotnet build BalanceBoard.sln -c Release -warnaserror
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Test-ProjectCounts -NoBuild
Write-Host "verify-tests passed."
