# Validate version consistency across Directory.Build.props, CHANGELOG.md, and an optional git tag.
param(
    [string]$Tag = ""
)

$ErrorActionPreference = "Stop"
$Root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Set-Location $Root

$propsPath = Join-Path $Root "Directory.Build.props"
$changelogPath = Join-Path $Root "CHANGELOG.md"

$props = Get-Content $propsPath -Raw
$propsVersion = [regex]::Match($props, '<Version>([^<]+)</Version>').Groups[1].Value
if (-not $propsVersion) {
    throw "Could not read <Version> from Directory.Build.props"
}

$changelog = Get-Content $changelogPath -Raw
$changelogPattern = "## \[$([regex]::Escape($propsVersion))\]"
if ($changelog -notmatch $changelogPattern) {
    throw "CHANGELOG.md missing section '## [$propsVersion]'"
}

if ($Tag) {
    if ($Tag -match '^v') {
        $Tag = $Tag.Substring(1)
    }

    if ($Tag -ne $propsVersion) {
        throw "Tag version '$Tag' does not match Directory.Build.props version '$propsVersion'"
    }
}

$suffix = if ($Tag) { " + tag" } else { "" }
Write-Host "Version $propsVersion verified (Directory.Build.props + CHANGELOG.md$suffix)." -ForegroundColor Green
