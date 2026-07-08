# Fast release helper — assumes CI is already green on the commit you tag.
param(
    [string]$Tag = "",

    [switch]$Retag,

    [switch]$SkipCiCheck,

    [switch]$DispatchOnly
)

$ErrorActionPreference = "Stop"
$Root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Set-Location $Root

function Get-VersionFromProps {
    $propsPath = Join-Path $Root "Directory.Build.props"
    return [regex]::Match((Get-Content $propsPath -Raw), '<Version>([^<]+)</Version>').Groups[1].Value
}

function Test-MainCiGreen {
    param([string]$Commit = "HEAD")

    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        Write-Warning "gh CLI not found — skipping CI check."
        return $true
    }

    $runs = gh run list --workflow ci.yml --commit $Commit --limit 5 --json databaseId,conclusion,status 2>$null | ConvertFrom-Json
    if (-not $runs) {
        Write-Warning "No CI runs found for $Commit."
        return $false
    }

    $success = $runs | Where-Object { $_.conclusion -eq "success" }
    if ($success) {
        Write-Host "CI passed on $Commit (run $($success[0].databaseId))." -ForegroundColor Green
        return $true
    }

    Write-Host "CI not green on $Commit. Latest runs:" -ForegroundColor Red
    $runs | ForEach-Object { Write-Host "  $($_.databaseId) status=$($_.status) conclusion=$($_.conclusion)" }
    return $false
}

if (-not $Tag) {
    $Tag = "v$(Get-VersionFromProps)"
}

if ($Tag -notmatch '^v') {
    $Tag = "v$Tag"
}

$branch = git rev-parse --abbrev-ref HEAD
if ($branch -ne "main") {
    throw "Checkout main before releasing (current: $branch)."
}

if (-not $SkipCiCheck -and -not (Test-MainCiGreen)) {
    throw "CI is not green. Fix CI or pass -SkipCiCheck to override."
}

if ($DispatchOnly) {
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        throw "gh CLI required for -DispatchOnly."
    }

    gh workflow run release.yml -f "tag=$Tag"
    Write-Host "Triggered release workflow for $Tag (workflow_dispatch)." -ForegroundColor Green
    Write-Host "Watch: gh run list --workflow release.yml --limit 3" -ForegroundColor Cyan
    exit 0
}

if ($Retag) {
    Write-Host "Removing existing tag $Tag (local + remote)..." -ForegroundColor Yellow
    git tag -d $Tag 2>$null
    git push origin ":refs/tags/$Tag" 2>$null
}

if (-not (git tag -l $Tag)) {
    git tag -a $Tag -m "Release $Tag"
    Write-Host "Created tag $Tag on $(git rev-parse --short HEAD)" -ForegroundColor Green
}
else {
    Write-Host "Tag $Tag already exists on $(git rev-parse --short $Tag^{commit})" -ForegroundColor Yellow
}

Write-Host "Pushing tag $Tag..." -ForegroundColor Cyan
git push origin $Tag

Write-Host ""
Write-Host "Release workflow started (package + upload only, ~2-4 min)." -ForegroundColor Green
Write-Host "  gh run list --workflow release.yml --limit 3" -ForegroundColor Cyan
Write-Host ""
Write-Host "Re-publish without re-tagging: .\scripts\release\quick-release.ps1 -Tag $Tag -DispatchOnly" -ForegroundColor DarkGray
