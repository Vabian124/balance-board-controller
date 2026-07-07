# AGENTS.md — AI assistant guide

> **Start here.** This file orients coding agents (Cursor, Copilot, Claude, etc.) to the Balance Board Controller repo.

> **Every pass:** read [INSTRUCTIONS.md](INSTRUCTIONS.md) for commit rules, maintenance checklist, and fork/license notes.

## What this project is

Windows desktop app (.NET 8, WPF) that reads a **Nintendo Wii Fit Balance Board** over Bluetooth and outputs:

1. **vJoy virtual game controller** axes (primary use case: gaming)
2. **Keyboard / mouse** via `SendInput` (hand-free desktop preset)

Upstream inspiration: [WiiBalanceWalker v0.5](https://github.com/lshachar/WiiBalanceWalker). This repo is a clean rewrite with modern UI, diagnostics, and safer vJoy handling.

| Item | Value |
|------|-------|
| GitHub | `https://github.com/Vabian124/balance-board-controller` |
| Solution | `BalanceBoard.sln` |
| Main app | `src/BalanceBoard.App/` → `BalanceBoardApp.exe` |
| Core library | `src/BalanceBoard.Core/` (no WPF dependency) |
| Platform | **Windows x64 only** |
| Target framework | `net8.0-windows` |

## Read order for new agents

1. [docs/CODEMAP.md](docs/CODEMAP.md) — every source file and its job
2. [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) — data flow, threading, extension points
3. [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) — build, run, debug, CI
4. [docs/GLOSSARY.md](docs/GLOSSARY.md) — domain terms
5. [docs/ROADMAP.md](docs/ROADMAP.md) — planned work (safe to implement)
6. [docs/updates/README.md](docs/updates/README.md) — **what past agents did** (timestamped, per commit)

## Architecture in 30 seconds

```
WiimoteLib (BT/HID)
    → BalanceBoardConnection  (raw BalanceReading)
    → BalanceProcessor        (ProcessedBalance: lean, triggers, joy axes)
    → VJoyController          (if EnableVJoy)
    → InputSimulator          (if !DisableKeyboardActions)
```

Orchestrator: `BalanceBoardSession` (50 ms poll timer).  
UI: `MainWindow` binds to session events; settings via `SettingsStore` → `%AppData%\BalanceBoardApp\settings.json`.

## Where to change common things

| Goal | Primary files |
|------|----------------|
| Add / edit output profile | `ActionPresets.cs`, `BalanceBoardSession.cs` |
| Change lean → axis math | `BalanceProcessor.cs` |
| Change keyboard/mouse output | `InputSimulator.cs`, `AppSettings.Actions` |
| vJoy acquire / release | `VJoyController.cs`, `FeederProcessCleanup.cs` |
| Board connect / tare | `BalanceBoardConnection.cs` |
| Dashboard UI | `MainWindow.xaml`, `MainWindow.xaml.cs` |
| Setup flow | `SetupWizardWindow.xaml(.cs)` |
| Health check / diagnostics | `DiagnosticsReport.cs`, `tools/Validate/Program.cs` |
| Persist settings | `SettingsStore.cs`, `AppSettings.cs` |
| Startup / single instance | `App.xaml.cs` |
| Theme / styles | `Themes/ModernTheme.xaml` |

## Conventions (follow these)

- **Layering**: UI in `BalanceBoard.App`; all device/logic in `BalanceBoard.Core`. App references Core only.
- **Namespaces**: file-scoped (`namespace X;`), nullable enabled.
- **Settings**: mutate `AppSettings`, then `SettingsStore.Save` + `BalanceBoardSession.LoadSettings`.
- **Presets**: use `ActionPresets.Apply*` — do not duplicate binding logic in UI.
- **vJoy safety**: always `RelinquishVJD` on shutdown; use `FeederProcessCleanup` before acquire.
- **UI guards**: `_uiReady` and `_suppressSettingEvents` in `MainWindow` prevent save loops during init.
- **Logs**: use `FileLogService` / session `Log` event — never commit `%AppData%` logs.
- **Native DLLs**: `libs/x64/` (`WiimoteLib.dll`, `vJoyInterface.dll`, `vJoyInterfaceWrap.dll`). Copied to output via csproj.
- **Scope**: minimal diffs; match existing naming and patterns.

## Do not

- Commit `baseline/`, `reference/`, `*.log`, or user `%AppData%` data
- Add heavy abstractions for one-off helpers
- Break single-instance / feeder cleanup (causes vJoy `VJD_STAT_BUSY`)
- Retarget away from Windows x64 without explicit request
- Push to `lshachar/WiiBalanceWalker` (upstream reference only)

## Build & verify

```powershell
dotnet build BalanceBoard.sln -c Release
dotnet run --project tools/Validate/BalanceBoard.Validate.csproj -c Release
dotnet run --project src/BalanceBoard.App/BalanceBoard.App.csproj -c Release
```

CI: `.github/workflows/build.yml` (Windows, Release build + validate tool).

## Key models

- `BalanceReading` — raw kg per corner from WiimoteLib
- `ProcessedBalance` — derived lean %, movement booleans, vJoy axis shorts
- `AppSettings` — triggers, vJoy flags, `Actions` dictionary (8 slots)
- `ActionBinding` — `Key`, `MouseButton`, `MouseMoveX/Y`, or `None`

Action slot keys: `Left`, `Right`, `Forward`, `Backward`, `Modifier`, `Jump`, `DiagonalLeft`, `DiagonalRight`.

## External dependencies

| Dependency | Role |
|------------|------|
| [vJoy](https://github.com/shauleiz/vJoy) | Virtual joystick driver (user must install + reboot) |
| WiimoteLib | Wii HID / balance board protocol |
| `user32.dll` SendInput | Keyboard/mouse simulation |

## Getting unstuck

| Symptom | Check |
|---------|-------|
| vJoy busy | `FeederProcessCleanup`, close vJoy Monitor, single instance |
| No devices | Windows Bluetooth pairing, SYNC button, `DiscoverDevices()` |
| DLL mismatch warning | Replace `libs/x64/vJoyInterface*.dll` from vJoy install |
| UI crash on startup | Settings loaded before `InitializeComponent()` in `MainWindow` ctor |

Run **Debug Suite → Health Check** or the Validate CLI for a full report.
