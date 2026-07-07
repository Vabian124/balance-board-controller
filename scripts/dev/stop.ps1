# Stop Balance Board Controller gracefully, then force if needed
$procs = Get-Process -Name "BalanceBoardApp" -ErrorAction SilentlyContinue
if (-not $procs) {
    Write-Host "Balance Board Controller is not running."
    exit 0
}

Write-Host "Stopping Balance Board Controller..."
foreach ($p in $procs) {
    $p.CloseMainWindow() | Out-Null
}

Start-Sleep -Seconds 2

$remaining = Get-Process -Name "BalanceBoardApp" -ErrorAction SilentlyContinue
if ($remaining) {
    Write-Host "Force stopping..."
    $remaining | Stop-Process -Force
}

Write-Host "Stopped."
