# Development guide

For AI agents and human contributors working on Balance Board Controller.

## Prerequisites

- Windows 10/11 x64
- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- vJoy driver installed (for runtime testing)
- Wii Balance Board paired via Windows Bluetooth (for hardware tests)

## Commands

```powershell
# Restore + build everything
dotnet build BalanceBoard.sln -c Release

# Run GUI
dotnet run --project src/BalanceBoard.App/BalanceBoard.App.csproj -c Release

# CLI health check (no GUI)
dotnet run --project tools/Validate/BalanceBoard.Validate.csproj -c Release

# Format (optional)
dotnet format BalanceBoard.sln

# Full lint + static analysis (format, build, unified tests)
.\scripts\lint.ps1
# or directly:
.\scripts\ci\lint.ps1

# Unified test pipeline only
.\scripts\ci\test.ps1
.\scripts\ci\test.ps1 -Quick
.\scripts\test\run-all.ps1

# Dev scripts live under scripts/dev/
.\scripts\dev\start.ps1
.\scripts\dev\stop.ps1
.\scripts\dev\test-flow.ps1

# Publish self-contained folder
dotnet publish src/BalanceBoard.App/BalanceBoard.App.csproj -c Release -r win-x64 --self-contained
```

Output EXE: `src/BalanceBoard.App/bin/Release/net8.0-windows/BalanceBoardApp.exe`

## Project boundaries

| Project | References | Must not |
|---------|------------|----------|
| `BalanceBoard.App` | Core | Contain device/vJoy logic |
| `BalanceBoard.Core` | WiimoteLib, vJoy wrap | Reference WPF |
| `BalanceBoard.Validate` | Core | Duplicate diagnostics logic |
| `BalanceBoard.UiSmoke` | App | Headless MainWindow load test |

## Adding a feature — checklist

1. Read [CODEMAP.md](CODEMAP.md) — pick the right file
2. If settings change: update `AppSettings`, `SettingsStore` still works via JSON
3. If output behavior changes: prefer `BalanceProcessor` or `ActionPresets`
4. Wire UI in `MainWindow` with `_suppressSettingEvents` guard
5. `dotnet build BalanceBoard.sln -c Release` — zero errors
6. Test with Validate tool if vJoy/HID touched

## Debugging

### vJoy issues

```powershell
dotnet run --project tools/Validate/BalanceBoard.Validate.csproj -c Release
```

Or use app **Debug Suite → Run Health Check**.

Common statuses:

| Status | Meaning |
|--------|---------|
| `VJD_STAT_FREE` | Device available |
| `VJD_STAT_OWN` | We hold it |
| `VJD_STAT_BUSY` | Another feeder holds it — run cleanup |
| `VJD_STAT_MISS` | Device not configured in vJoyConf |

### Session logs

```
%AppData%\BalanceBoardApp\logs\session-YYYY-MM-DD.log
```

### Single instance during dev

Production `start.bat` enforces single instance. Use `scripts/dev/start.ps1 --dev` for parallel instances. Be aware of mutex + feeder cleanup behavior in `App.xaml.cs`.

## CI

`.github/workflows/ci.yml` (same as `.\scripts\lint.ps1` / `.\scripts\ci\lint.ps1`):

- Trigger: push/PR to `main`, or manual **workflow_dispatch**
- Runner: `windows-latest`
- Steps: crash-safety grep → **format check** → Release build (`-warnaserror`) → **unified test pipeline** (`scripts/ci/test.ps1`) → upload `artifacts/test/`

**Releases:** see [RELEASING.md](RELEASING.md). Tag push runs `release.yml` (package + upload only, ~2–4 min). Use `.\scripts\release\quick-release.ps1` after CI is green.

### Linting and static analysis

| Tool | What it catches |
|------|-----------------|
| `dotnet format` | C# style, import order |
| Built-in .NET analyzers | Nullable, code quality (`AnalysisLevel=latest`) |
| [WpfAnalyzers](https://www.nuget.org/packages/WpfAnalyzers) | WPF dependency property mistakes (on `BalanceBoard.App`) |
| `tools/UiSmoke` | Legacy minimal MainWindow load (superseded by `BalanceBoard.App.Ui.Tests`) |
| `tools/Validate` | vJoy driver, HID discovery |
| `scripts/ci/test.ps1` | Unified test pipeline + `artifacts/test/` logs |
| `scripts/ci/lint.ps1` | Runs format, build, and unified test pipeline |
| `scripts/lint.ps1` | Thin wrapper → `scripts/ci/lint.ps1` |

**Note:** No existing XAML linter reliably validates `StaticResource` type compatibility at compile time on .NET 8. **Headless WPF UI tests** (`BalanceBoard.App.Ui.Tests`) are the practical guard.

Optional IDE extension: [Rapid XAML Toolkit](https://marketplace.visualstudio.com/items?itemName=MattLaceyLtd.RapidXamlAnalysis) for in-editor XAML hints (not in CI — `RapidXaml.BuildAnalysis` NuGet is broken on SDK-style projects).

## Native DLLs

Shipped in `libs/x64/`. If vJoy driver version differs from bundled DLLs:

1. Copy from vJoy install directory (often `C:\Program Files\vJoy\x64\`)
2. Replace `vJoyInterface.dll` and `vJoyInterfaceWrap.dll`
3. Rebuild

Mismatch shows as warning in diagnostics; may still work.

## Code style

- File-scoped namespaces
- Nullable reference types enabled
- `.editorconfig` + `Directory.Build.props` enforce style on build
- Keep changes minimal and localized
- XML doc comments on non-obvious public APIs (optional but welcome for services)

## Testing without hardware

- Validate tool: vJoy driver + DLL checks
- **UiSmoke tool:** legacy minimal loader; use `BalanceBoard.App.Ui.Tests` for CI
- `scripts/lint.ps1` or `scripts/ci/lint.ps1`: full pre-commit check suite
- `DiscoverDevices()` returns empty if no Wii HID — expected
- Unit/integration/fuzz/automation tests under `tests/` — see [TESTING.md](TESTING.md)

## Git hygiene

**Never commit:**

- `baseline/`, loose binaries in `reference/` (tracked legacy source is OK)
- `*.log`, `logs/`
- `bin/`, `obj/`, `.vs/`
- User settings from `%AppData%`
