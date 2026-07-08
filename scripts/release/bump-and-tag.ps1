# Bump version in Directory.Build.props and create an annotated git tag.
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [switch]$Commit,

    [switch]$NoTag
)

$ErrorActionPreference = "Stop"
$Root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Set-Location $Root

if ($Version -match '^v') {
    $Version = $Version.Substring(1)
}

if ($Version -notmatch '^\d+\.\d+\.\d+') {
    throw "Version must be semver like 1.4.0 (got '$Version')."
}

$tag = "v$Version"
$propsPath = Join-Path $Root "Directory.Build.props"
$props = Get-Content $propsPath -Raw

$current = [regex]::Match($props, '<Version>([^<]+)</Version>').Groups[1].Value
if ($current -eq $Version) {
    Write-Host "Version already $Version in Directory.Build.props." -ForegroundColor Yellow
}
else {
    $props = $props -replace '<Version>[^<]+</Version>', "<Version>$Version</Version>"
    $props = $props -replace '<AssemblyVersion>[^<]+</AssemblyVersion>', "<AssemblyVersion>$Version.0</AssemblyVersion>"
    $props = $props -replace '<FileVersion>[^<]+</FileVersion>', "<FileVersion>$Version.0</FileVersion>"
    Set-Content -Path $propsPath -Value $props -NoNewline
    Write-Host "Bumped version $current -> $Version" -ForegroundColor Green
}

Write-Host ""
Write-Host "REMINDER: update CHANGELOG.md with a [$Version] section before tagging." -ForegroundColor Cyan
Write-Host "  docs/updates/README.md — one-line agent log after commit." -ForegroundColor Cyan
Write-Host ""

if ($Commit) {
    git add $propsPath
    git commit -m "Release $tag: bump version to $Version."
    Write-Host "Committed version bump." -ForegroundColor Green
}

if (-not $NoTag) {
    if (git tag -l $tag) {
        throw "Tag $tag already exists locally. Delete it first or use quick-release.ps1 -Retag."
    }

    git tag -a $tag -m "Release $tag"
    Write-Host "Created annotated tag $tag" -ForegroundColor Green
    Write-Host "Push with: git push origin main; git push origin $tag" -ForegroundColor Cyan
}
