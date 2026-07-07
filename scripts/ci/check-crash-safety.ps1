# Crash-safety grep — fails CI if forbidden crash patterns appear in product code.
$ErrorActionPreference = "Stop"
$Root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Set-Location $Root

$files = @(
    "src/BalanceBoard.App",
    "src/BalanceBoard.Core"
) | ForEach-Object { Get-ChildItem -Path (Join-Path $Root $_) -Recurse -Filter "*.cs" }

$patterns = @(
    @{ Name = "NotImplementedException"; Regex = "NotImplementedException" },
    @{ Name = "Shutdown(-1)"; Regex = "Shutdown\s*\(\s*-" },
    @{ Name = "Environment.Exit"; Regex = "Environment\.Exit" }
)

$failed = $false
foreach ($pattern in $patterns) {
    $hits = $files | Select-String -Pattern $pattern.Regex
    if ($hits) {
        $failed = $true
        Write-Host "FAIL: $($pattern.Name) found:" -ForegroundColor Red
        $hits | ForEach-Object { Write-Host "  $($_.Path):$($_.LineNumber): $($_.Line.Trim())" }
    }
}

if ($failed) {
    exit 1
}

Write-Host "check-crash-safety: OK (no forbidden patterns in src/)"
