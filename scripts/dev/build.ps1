# Build Release output (framework-dependent win-x64 folder).
param(
    [switch]$Publish,
    [switch]$SelfContained
)

$ErrorActionPreference = "Stop"
$Root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
Set-Location $Root

Write-Host "=== dotnet build (Release) ===" -ForegroundColor Cyan
dotnet build BalanceBoard.sln -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$exe = Join-Path $Root "src\BalanceBoard.App\bin\Release\net8.0-windows\BalanceBoardApp.exe"
Write-Host "Built: $exe" -ForegroundColor Green

if ($Publish) {
    $out = Join-Path $Root "dist\BalanceBoardController-win-x64"
    $args = @(
        "publish", "src/BalanceBoard.App/BalanceBoard.App.csproj",
        "-c", "Release",
        "-r", "win-x64",
        "-o", $out
    )
    if ($SelfContained) {
        $args += "--self-contained", "true"
    } else {
        $args += "--self-contained", "false"
    }

    Write-Host "`n=== dotnet publish ===" -ForegroundColor Cyan
    dotnet @args
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Write-Host "Published: $out" -ForegroundColor Green
}
