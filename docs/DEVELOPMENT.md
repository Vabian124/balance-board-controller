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

App kills prior `BalanceBoardApp` instances on startup. If debugging multiple instances, be aware of mutex + feeder cleanup behavior in `App.xaml.cs`.

## CI

`.github/workflows/build.yml`:

- Trigger: push/PR to `main`
- Runner: `windows-latest`
- Steps: restore → Release build → run Validate tool

Validate step builds via `dotnet run --no-restore` (compiles Validate if needed).

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
- `DiscoverDevices()` returns empty if no Wii HID — expected
- Unit tests: none yet; add under `tests/` if needed (not present today)

## Git hygiene

**Never commit:**

- `baseline/`, `reference/` (local comparison artifacts)
- `*.log`, `logs/`
- `bin/`, `obj/`, `.vs/`
- User settings from `%AppData%`
