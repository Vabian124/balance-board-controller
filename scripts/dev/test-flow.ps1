# Smoke tests for app lifecycle (no balance board required)
$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
Set-Location $root

Write-Host "=== Build ==="
dotnet build BalanceBoard.sln -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$exe = Join-Path $root "src\BalanceBoard.App\bin\Release\net8.0-windows\BalanceBoardApp.exe"
if (-not (Test-Path $exe)) {
    Write-Error "Executable not found: $exe"
}

function Stop-App {
    & (Join-Path $PSScriptRoot "stop.ps1")
}

Write-Host "`n=== Stop any existing instance ==="
Stop-App
Start-Sleep 1

Write-Host "`n=== Start (dev mode) ==="
$env:BALANCEBOARD_DEV = "1"
$proc = Start-Process -FilePath $exe -ArgumentList "--dev" -PassThru
Start-Sleep 3

if ($proc.HasExited) {
    Write-Error "App exited immediately (code $($proc.ExitCode))"
}

$running = Get-Process -Name "BalanceBoardApp" -ErrorAction SilentlyContinue
if (-not $running) {
    Write-Error "BalanceBoardApp process not found after start"
}
Write-Host "OK: process running (PID $($running.Id))"

$logDir = Join-Path $env:APPDATA "BalanceBoardApp\logs"
if (-not (Test-Path $logDir)) {
    Write-Warning "Log directory not created yet: $logDir"
} else {
    Write-Host "OK: log directory exists"
}

Write-Host "`n=== Second instance (dev allows multiple) ==="
$proc2 = Start-Process -FilePath $exe -ArgumentList "--dev" -PassThru
Start-Sleep 2
$count = (Get-Process -Name "BalanceBoardApp" -ErrorAction SilentlyContinue).Count
if ($count -lt 2) {
    Write-Warning "Expected 2 dev instances, found $count"
} else {
    Write-Host "OK: dev mode allows multiple instances ($count)"
}
Stop-App
if ($proc2 -and -not $proc2.HasExited) { $proc2 | Stop-Process -Force -ErrorAction SilentlyContinue }

Write-Host "`n=== Single-instance mode ==="
Remove-Item Env:BALANCEBOARD_DEV -ErrorAction SilentlyContinue
$proc = Start-Process -FilePath $exe -PassThru
Start-Sleep 3
$before = (Get-Process -Name "BalanceBoardApp" -ErrorAction SilentlyContinue).Count
Start-Process -FilePath $exe -PassThru | Out-Null
Start-Sleep 2
$after = (Get-Process -Name "BalanceBoardApp" -ErrorAction SilentlyContinue).Count
if ($after -gt $before) {
    Write-Error "Second launch created extra process (before=$before after=$after)"
}
Write-Host "OK: single-instance guard (process count=$after)"

Write-Host "`n=== Stop ==="
Stop-App
Start-Sleep 2
$left = Get-Process -Name "BalanceBoardApp" -ErrorAction SilentlyContinue
if ($left) {
    Write-Error "Process still running after stop"
}
Write-Host "OK: stopped cleanly"

Write-Host "`n=== All smoke tests passed ==="
