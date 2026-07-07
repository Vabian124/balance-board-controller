# Portfolio repo organization and CI quality gate

**Date:** 2026-07-07  
**Commit:** (see git log)

## Summary

Professional portfolio cleanup: pinned SDK, Roslyn analyzers, unified CI quality gate, script layout, and documentation polish.

## Changes

- **`global.json`** — pin .NET 8 SDK
- **`Directory.Build.props`** — NetAnalyzers, Release `TreatWarningsAsErrors`, shared NoWarn for style noise
- **`scripts/ci/lint.ps1`** — canonical quality gate (format, build, 41 tests, Validate, UiSmoke, lifecycle)
- **`scripts/dev/`** — start, stop, restart, connect, test-flow (root `scripts/*.ps1` forward for compatibility)
- **`.github/workflows/ci.yml`** — runs full lint gate; replaced `build.yml`
- **`.github/dependabot.yml`**, PR template, **SECURITY.md**, **CHANGELOG.md**
- **README** — CI badge, layout table, quality section
- **`.vscode/`** — launch + tasks for contributors
- **`docs/testing/README.md`** — test pyramid documentation
- Removed dead **`StaThread.cs`** (superseded by `ConnectionWorker`)
- dotnet format pass on codebase

## Verification

```powershell
.\scripts\lint.ps1
```
