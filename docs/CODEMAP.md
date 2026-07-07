# CODEMAP — file reference

Complete map of **maintained source** (ignore `bin/`, `obj/`, `.vs/`).

## Solution & config

| Path | Purpose |
|------|---------|
| `BalanceBoard.sln` | App + Core + Validate projects |
| `Directory.Build.props` | Shared nullable, analyzers, code style |
| `.editorconfig` | Formatting rules |
| `.github/workflows/build.yml` | CI: restore, Release build, validate tool |
| `.gitignore` | Excludes logs, baseline, reference, build output |

## `src/BalanceBoard.App/` — WPF application

| Path | Purpose |
|------|---------|
| `App.xaml` | Application resources, theme merge |
| `App.xaml.cs` | Instant UI, deferred startup, single-instance via `SingleInstanceService` |
| `MainWindow.xaml` | Single-page dashboard: settings expanders, connect/cancel, debug log |
| `MainWindow.xaml.cs` | UI ↔ session; smart connect policy; cancellable connect |
| `Services/StartupOptions.cs` | CLI: `--dev`, `--connect`, `--no-cleanup`, `--allow-multiple` |
| `Services/SingleInstanceService.cs` | Mutex + named pipe; second launch activates window |
| `Controls/BalanceBoardVisual.xaml(.cs)` | Live 2D balance dot visualization |
| `Themes/Colors.xaml` | System light/dark aware brushes |
| `Themes/Controls.xaml` | Shared control styles |
| `BalanceBoard.App.csproj` | WPF exe, copies native DLLs from `libs/x64/` |

### UI event flow (`MainWindow.xaml.cs`)

- Constructor: `SettingsStore.Load()` → `session.LoadSettings(initializeVJoy: false)` → instant UI
- `RunDeferredStartup`: warmup, vJoy init, quick reconnect or welcome message
- `BeginConnect(ConnectionIntent)`: QuickReconnect vs PairAndConnect

## `src/BalanceBoard.Core/` — business logic

### Models (`Models/`)

| Path | Purpose |
|------|---------|
| `AppSettings.cs` | All user settings; `Actions` dict; trigger thresholds |
| `ActionPresets.cs` | Named profiles: Game Controller, Pedal, Hand-Free Desktop |
| `ActionBinding.cs` | In `AppSettings.cs` — Key/MouseButton/MouseMove binding |
| `BalanceReading.cs` | Raw sensor snapshot from WiimoteLib |
| `ProcessedBalance.cs` | Processed lean, movement flags, vJoy axis values |

### Services (`Services/`)

| Path | Purpose |
|------|---------|
| `BalanceBoardSession.cs` | **Orchestrator**: `ConnectWithIntent`, poll (50 ms), route to vJoy + input |
| `ConnectionIntent.cs` | `QuickReconnect` vs `PairAndConnect` |
| `BalanceBoardConnection.cs` | WiimoteLib wrapper: discover, connect, tare, read corners |
| `BalanceProcessor.cs` | Tare, center offset, deadzone, triggers → `ProcessedBalance` |
| `VJoyController.cs` | Acquire vJoy device, map axes, center on shutdown |
| `VJoyDiagnostics.cs` | Read driver status, axis capabilities, DLL version match |
| `InputSimulator.cs` | `SendInput` keyboard/mouse; supports X1/X2 side buttons |
| `BluetoothPairingService.cs` | Automatic Wii BT pairing (reversed host MAC PIN, WiiBalanceWalker method) |
| `WiiBluetoothPin.cs` | Host MAC → Wii permanent pairing PIN |
| `SettingsStore.cs` | JSON settings in `%AppData%\BalanceBoardApp\` |
| `FileLogService.cs` | Session log file `logs/session-YYYY-MM-DD.log` |
| `DiagnosticsReport.cs` | Structured health check for UI + clipboard |

## `tools/Validate/`

| Path | Purpose |
|------|---------|
| `Program.cs` | CLI: feeder cleanup, vJoy diag, HID discovery, axis sweep test |
| `BalanceBoard.Validate.csproj` | Console app referencing Core |

## `libs/x64/`

| File | Purpose |
|------|---------|
| `WiimoteLib.dll` | Wii HID communication |
| `vJoyInterface.dll` | vJoy native driver API |
| `vJoyInterfaceWrap.dll` | Managed wrapper used by `VJoyController` |

## `WiiBalanceWalker/` (legacy reference)

Original MS-PL project. Use for behavioral reference only. **Do not** treat as the active app entry point.

## Runtime paths (not in repo)

| Path | Content |
|------|---------|
| `%AppData%\BalanceBoardApp\settings.json` | User settings |
| `%AppData%\BalanceBoardApp\profiles\` | Saved profile JSON files |
| `%AppData%\BalanceBoardApp\logs\` | Session logs |

## Dependency graph

```
BalanceBoard.App
  └── BalanceBoard.Core
        ├── WiimoteLib.dll
        └── vJoyInterfaceWrap.dll → vJoyInterface.dll

BalanceBoard.Validate
  └── BalanceBoard.Core
```
