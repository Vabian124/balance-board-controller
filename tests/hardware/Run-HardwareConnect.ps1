# Hardware connect smoke: requires a paired Wii Balance Board. Fails on [ERROR] or FATAL in log after connect.
$ErrorActionPreference = "Stop"
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Set-Location $root

& (Join-Path $PSScriptRoot "Detect-Board.ps1")
if ($LASTEXITCODE -ne 0) {
    Write-Host "Skipping hardware connect — no board detected."
    exit 0
}

dotnet build BalanceBoard.sln -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$exe = Join-Path $root "src\BalanceBoard.App\bin\Release\net8.0-windows\BalanceBoardApp.exe"
$logDir = Join-Path $env:APPDATA "BalanceBoardApp\logs"
$before = Get-Date

$proc = Start-Process -FilePath $exe -ArgumentList "--connect --hardware-test-mode --auto-exit-after 15 --dev" -PassThru
if (-not $proc.WaitForExit(120000)) {
    Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
    Write-Error "Hardware connect test timed out."
}

if ($proc.ExitCode -ne 0) {
    Write-Error "App exited with code $($proc.ExitCode)."
}

$log = Get-ChildItem $logDir -Filter "session-*.log" |
    Where-Object { $_.LastWriteTime -ge $before.AddMinutes(-1) } |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if (-not $log) {
    Write-Error "No session log written during hardware test."
}

$content = Get-Content $log.FullName -Raw
if ($content -match '\[ERROR\]|FATAL') {
    Write-Error "Log contains errors after connect: $($log.FullName)"
}

if ($content -notmatch '\[CONNECT\].*First balance reading') {
    Write-Error "No balance readings observed in log."
}

Write-Host "Hardware connect test passed."
