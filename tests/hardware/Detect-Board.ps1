# Returns 0 when a Wii Balance Board HID device is visible (paired + awake).
$ErrorActionPreference = "Stop"
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Set-Location $root

dotnet build src/BalanceBoard.Core/BalanceBoard.Core.csproj -c Release -v q
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$code = @"
using BalanceBoard.Core.Services;
var ids = new BalanceBoardConnection().DiscoverDeviceIds();
Console.WriteLine(ids.Count);
foreach (var id in ids) Console.WriteLine(id);
"@

$tmp = Join-Path $env:TEMP "detect-board-$([guid]::NewGuid().ToString('N')).cs"
$tmpProj = Join-Path $env:TEMP "detect-board-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $tmpProj -Force | Out-Null
Set-Content -Path (Join-Path $tmpProj "Program.cs") -Value $code
dotnet new console -n DetectBoard -o $tmpProj --force | Out-Null
dotnet add (Join-Path $tmpProj "DetectBoard.csproj") reference (Join-Path $root "src\BalanceBoard.Core\BalanceBoard.Core.csproj") | Out-Null
$output = dotnet run --project (Join-Path $tmpProj "DetectBoard.csproj") -c Release 2>&1
Remove-Item -Recurse -Force $tmpProj -ErrorAction SilentlyContinue

$lines = @($output)
$count = 0
if ($lines.Count -gt 0) { [void][int]::TryParse($lines[0], [ref]$count) }

if ($count -gt 0) {
    Write-Host "Board detected ($count HID device(s))."
    $lines | Select-Object -Skip 1 | ForEach-Object { Write-Host "  $_" }
    exit 0
}

Write-Host "No balance board HID devices found."
exit 1
