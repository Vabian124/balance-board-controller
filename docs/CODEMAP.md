# CODEMAP — file reference

Complete map of **maintained source** (ignore `bin/`, `obj/`, `.vs/`).

## Solution & config

| Path | Purpose |
|------|---------|
| `BalanceBoard.sln` | App + Core + Validate + UiSmoke + Tests |
| `Directory.Build.props` | Shared nullable, analyzers, code style |
| `.editorconfig` | Formatting rules |
| `.github/workflows/ci.yml` | CI quality gate (`scripts/ci/lint.ps1`) |
| `.github/workflows/release.yml` | Tag release: lint gate + publish zip |
| `.gitignore` | Excludes logs, baseline, loose reference binaries, build output |

## `src/BalanceBoard.App/` — WPF application

| Path | Purpose |
|------|---------|
| `App.xaml` | Application resources, theme merge |
| `App.xaml.cs` | Instant UI, deferred startup, single-instance via `SingleInstanceService` |
| `MainWindow.xaml` | Tabbed UI: **Dashboard** (connect, balance visual), **Profiles** (presets, sensitivity, jump), **Advanced** (sliders, vJoy, bindings, debug) |
| `MainWindow.xaml.cs` | UI ↔ session; `UiDetailLevel` visibility; smart connect; cancellable connect |
| `Services/StartupOptions.cs` | CLI: `--dev`, `--connect`, `--simulate-board`, `--no-cleanup`, `--allow-multiple`, `--physical-test <scenario>` |
| `Services/SingleInstanceService.cs` | Mutex + named pipe; second launch activates window |
| `Services/ThemeManager.cs` | System / Light / Dark theme switching |
| `Services/PhysicalTestRunner.cs` | Opt-in manual hardware lane: guided scenarios, per-step outcomes, structured artifact writing |
| `Controls/BalanceBoardVisual.xaml(.cs)` | Live 2D balance dot, jump banner |
| `Controls/ActionBindingRow.xaml(.cs)` | Per-slot key/mouse binding editor row |
| `Dialogs/NamePromptDialog.xaml(.cs)` | Modal single-line text prompt (naming/importing custom profiles) |
| `Themes/Colors.xaml` | Shared brush keys |
| `Themes/Colors.Light.xaml` / `Colors.Dark.xaml` | Theme-specific palettes (dropdown contrast fix in v1.1.1) |
| `Themes/Controls.xaml` | Shared control styles, tab styles |
| `BalanceBoard.App.csproj` | WPF exe, copies native DLLs from `libs/x64/` |

### UI event flow (`MainWindow.xaml.cs`)

- Constructor: `SettingsStore.Load()` → `session.LoadSettings(initializeVJoy: false)` → instant UI
- `RunDeferredStartup`: warmup, vJoy init, quick reconnect or welcome message
- `BeginConnect(ConnectionIntent)`: QuickReconnect vs PairAndConnect
- `ApplyDetailLevelVisibility()`: show/hide Advanced tab and panels per `UiDetailLevel`

## `src/BalanceBoard.Core/` — business logic

### Models (`Models/`)

| Path | Purpose |
|------|---------|
| `AppSettings.cs` | All user settings; `Actions` dict; `UiDetailLevel`, `JumpLevel`, theme |
| `ActionSlots.cs` | Canonical action slot names (Left, Right, …) |
| `BalanceConstants.cs` | Thresholds, joy scaling, poll interval — **port verbatim** |
| `ActionConstants.cs` | Mouse-move timer interval for action engine |
| `VirtualKeyCodes.cs` | Key name → VK code lookup (no WinForms) |
| `ActionPresets.cs` | Named profiles: Game Controller, Minecraft, Pedal, Desktop, Balance Mouse |
| `JumpPresets.cs` | Easy / Normal / Hard jump thresholds + `UiDetailLevel` enum |
| `SensitivityPresets.cs` | Low / Medium / High / Highly sensitive bundles |
| `DeviceIdRules.cs` | `ExtractFromHidPath`, simulated ID rules, persist guards |
| `ActionBinding.cs` | In `AppSettings.cs` — Key/MouseButton/MouseMove binding |
| `BalanceReading.cs` | Raw sensor snapshot from WiimoteLib |
| `ProcessedBalance.cs` | Processed lean, movement flags, vJoy axis values |

### Processing (`Processing/`) — pure, Python-portable

| Path | Purpose |
|------|---------|
| `BalanceMath.cs` | Stateless balance math (deadzone, triggers, axes) |
| `BalanceDisplay.cs` | Balance dot position / direction text (shared with unit tests) |
| `MovementMapper.cs` | `ProcessedBalance` flags → `ActionSlots` |
| `ActionEngine.cs` | Portable action-slot state machine |

### Abstractions (`Abstractions/`)

| Path | Purpose |
|------|---------|
| `IGameControllerOutput.cs` | vJoy adapter contract for session + Python `ports.py` |
| `IActionSimulator.cs` | Keyboard/mouse action contract |
| `IInputBackend.cs` | Low-level key/mouse injection contract |

### Services (`Services/`)

| Path | Purpose |
|------|---------|
| `BalanceBoardSession.cs` | **Orchestrator**: `ConnectWithIntent`, poll on `ConnectionWorker` (50 ms), route to vJoy + input |
| `ConnectionWorker.cs` | Dedicated STA thread for WiimoteLib / BT / poll timer |
| `ConnectionIntent.cs` | `QuickReconnect` vs `PairAndConnect` |
| `BalanceBoardConnection.cs` | WiimoteLib wrapper: discover, connect, tare, read corners; disconnect hardening |
| `BalanceProcessor.cs` | Stateful tare/center; delegates math to `BalanceMath` |
| `VJoyController.cs` | `IGameControllerOutput` — acquire vJoy, coalesced `WriteAxes` |
| `VJoyDiagnostics.cs` | Read driver status, axis capabilities, DLL version match |
| `InputSimulator.cs` | Facade: `ActionEngine` + `Win32InputBackend` |
| `Win32InputBackend.cs` | `SendInput` keyboard/mouse (Windows only) |
| `BluetoothPairingService.cs` | Automatic Wii BT pairing (reversed host MAC PIN) |
| `WiiBluetoothPin.cs` | Host MAC → Wii permanent pairing PIN |
| `SettingsStore.cs` | JSON settings in `%AppData%\BalanceBoardApp\`; atomic save; connection state |
| `AppDataPaths.cs` | Canonical paths for settings, logs, profiles |
| `FileLogService.cs` | Daily session log with SESSION header + structured tags |
| `DiagnosticsReport.cs` | Structured health check for UI + clipboard |

## `tools/Validate/`

| Path | Purpose |
|------|---------|
| `Program.cs` | CLI: feeder cleanup, vJoy diag, HID discovery, axis sweep test |
| `BalanceBoard.Validate.csproj` | Console app referencing Core |

## `tools/UiSmoke/`

| Path | Purpose |
|------|---------|
| `Program.cs` | STA-thread `MainWindow` load + Minecraft preset — catches XAML/theme errors |
| `BalanceBoard.UiSmoke.csproj` | Console app referencing App |

## `scripts/`

| Path | Purpose |
|------|---------|
| `lint.ps1` | Entry point → delegates to `scripts/ci/lint.ps1` |
| `ci/lint.ps1` | Full gate: format, build, unified test pipeline |
| `ci/test.ps1` | Layered tests + `artifacts/test/` structured logs |
| `test/run-all.ps1` | Local wrapper → `ci/test.ps1` |
| `ci/verify-tests.ps1` | Meta: test projects wired in solution |
| `ci/check-crash-safety.ps1` | Grep guard for unsafe patterns |
| `ci/publish-release.ps1` | Package win-x64 zip for GitHub Releases |
| `dev/start.ps1` / `stop.ps1` / `restart.ps1` / `connect.ps1` | Dev launch helpers |
| `dev/test-flow.ps1` | Deprecated wrapper → `scripts/ci/test.ps1 -Quick` |
| `dev/sync-vjoy-dlls.ps1` | Copy vJoy DLLs from install into `libs/x64/` |

## `tests/`

| Project | Purpose |
|---------|---------|
| `BalanceBoard.Core.Tests` | Unit: `BalanceMath`, `BalanceProcessor`, `JumpPresets`, `ActionEngine`, migrations |
| `BalanceBoard.Integration.Tests` | Session, disconnect, simulated board |
| `BalanceBoard.Fuzz.Tests` | FsCheck property tests |
| `BalanceBoard.Automation` | Deterministic app launch/session-log + `--simulate-board` smoke |
| `BalanceBoard.App.Ui.Tests` | Headless WPF UI: settings, profiles, detail levels, connect |
| `BalanceBoard.Testing` | Shared fakes for integration tests |
| `hardware/` | Optional scripts when physical board present |

## `libs/x64/`

| File | Purpose |
|------|---------|
| `WiimoteLib.dll` | Wii HID communication |
| `vJoyInterface.dll` | vJoy native driver API |
| `vJoyInterfaceWrap.dll` | Managed wrapper used by `VJoyController` |

## `reference/WiiBalanceWalker/` (legacy MS-PL)

Original WiiBalanceWalker v0.5 WinForms project. Use for behavioral reference only — **do not** treat as the active app. See [reference/README.md](../reference/README.md).

## Runtime paths (not in repo)

| Path | Content |
|------|---------|
| `%AppData%\BalanceBoardApp\settings.json` | User settings |
| `%AppData%\BalanceBoardApp\profiles\` | Saved profile JSON files |
| `%AppData%\BalanceBoardApp\logs\` | Session logs |
| `%AppData%\BalanceBoardApp\artifacts\physical-tests\` | Opt-in guided hardware test run artifacts (`run.json`, `events.jsonl`) |

## Dependency graph

```
BalanceBoard.App
  └── BalanceBoard.Core
        ├── WiimoteLib.dll
        └── vJoyInterfaceWrap.dll → vJoyInterface.dll

BalanceBoard.Validate
  └── BalanceBoard.Core

BalanceBoard.Core.Tests
  └── BalanceBoard.Core
```
