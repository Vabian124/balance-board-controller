# Start Balance Board Controller (dev mode: skip killing other instances)
$ErrorActionPreference = "Stop"
$Root = Resolve-Path (Join-Path $PSScriptRoot "..\..")

$env:BALANCEBOARD_DEV = "1"

$releaseExe = Join-Path $Root "src\BalanceBoard.App\bin\Release\net8.0-windows\BalanceBoardApp.exe"
if (Test-Path $releaseExe) {
    Write-Host "Starting Balance Board Controller..."
    Start-Process -FilePath $releaseExe -ArgumentList "--dev" -WorkingDirectory (Split-Path $releaseExe)
} else {
    Write-Host "Building and running..."
    Push-Location $Root
    dotnet run --project src/BalanceBoard.App/BalanceBoard.App.csproj -c Release -- --dev @args
    Pop-Location
}
