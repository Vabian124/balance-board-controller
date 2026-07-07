# Balance Board Controller

[![CI](https://github.com/Vabian124/balance-board-controller/actions/workflows/ci.yml/badge.svg)](https://github.com/Vabian124/balance-board-controller/actions/workflows/ci.yml)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/download)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows%20x64-0078D4?logo=windows)](https://github.com/Vabian124/balance-board-controller)

A production-quality **.NET 8** desktop app that turns a **Nintendo Wii Fit Balance Board** into a **virtual game controller** ([vJoy](https://github.com/shauleiz/vJoy)) or hand-free keyboard/mouse input.

Clean rewrite of [WiiBalanceWalker v0.5](https://github.com/lshachar/WiiBalanceWalker) with automatic Bluetooth pairing, structured connect flows, diagnostics, fuzz/integration tests, and crash-hardened device handling.

> **Contributors & agents:** [CONTRIBUTING.md](CONTRIBUTING.md) · [INSTRUCTIONS.md](INSTRUCTIONS.md) · [AGENTS.md](AGENTS.md) · [docs/](docs/)

## Features

- **Live dashboard** — balance visual, direction text, connection status
- **Game controller mode** — lean → vJoy X/Y (works with most joystick games)
- **Presets** — game controller, pedals/rudder, hand-free WASD desktop
- **Smart connect** — first launch pairs on demand; returning users auto-reconnect
- **Crash-safe connect** — dedicated STA `ConnectionWorker` for WiimoteLib (no thread-pool dispose races)
- **Debug Suite** — health check, session log, copy report
- **Quality gate** — format, Roslyn analyzers, unit/integration/fuzz/automation tests in CI

## Requirements

- Windows 10/11 (64-bit)
- [.NET 8 SDK](https://dotnet.microsoft.com/download) (pinned in [`global.json`](global.json))
- [vJoy](https://github.com/shauleiz/vJoy) — reboot after install
- Bluetooth adapter + Wii Fit Balance Board

## Quick start

1. Install **vJoy**, reboot, enable **Device 1** (X/Y) in vJoyConf.
2. **Double-click [`start.bat`](start.bat)** or run `.\scripts\dev\start.ps1`
3. Click **Connect**, press **SYNC** on the board (first time).
4. Stand on the board → **Tare** → verify in vJoy Monitor.

```powershell
git clone https://github.com/Vabian124/balance-board-controller.git
cd balance-board-controller
.\start.bat
```

### Simulate (no hardware)

```powershell
dotnet run --project src/BalanceBoard.App/BalanceBoard.App.csproj -c Release -- --simulate-board --dev
```

## Quality & CI

```powershell
.\scripts\lint.ps1          # full gate: format, analyzers, all tests, smoke
.\scripts\ci\test-all.ps1   # lint + optional -IncludeHardware
```

CI runs the same gate on every push/PR ([`.github/workflows/ci.yml`](.github/workflows/ci.yml)).

| Checks | Tool |
|--------|------|
| Format | `dotnet format --verify-no-changes` |
| Static analysis | .NET analyzers + `-warnaserror` (Release) |
| Tests | 22 unit · 14 integration · 4 fuzz · 1 automation |
| Tools | `Validate`, `UiSmoke`, lifecycle `test-flow` |

See [docs/testing/README.md](docs/testing/README.md).

## Repository layout

```
src/
  BalanceBoard.App/     WPF UI
  BalanceBoard.Core/    Device logic, processing, vJoy (no WPF)
tests/
  BalanceBoard.*.Tests/ Unit, integration, fuzz, automation
  BalanceBoard.Testing/ Shared fakes
  hardware/             Optional board scripts
tools/
  Validate/             CLI health check
  UiSmoke/              XAML load smoke
scripts/
  ci/                   lint.ps1, verify-tests.ps1, test-all.ps1
  dev/                  start, stop, restart, connect, test-flow
reference/              Legacy WiiBalanceWalker (MS-PL, not in solution)
libs/x64/               WiimoteLib + vJoy native DLLs
docs/                   Architecture, CODEMAP, testing guide
```

## Troubleshooting

| Problem | Fix |
|---------|-----|
| vJoy busy | App stops stale feeders; close vJoy Monitor |
| vJoy missing | Reboot after install; check Device Manager |
| Connect crash | See session log; ensure latest `main` (ConnectionWorker fix) |
| DLL mismatch | Copy `vJoyInterface*.dll` from your vJoy install into `libs/x64/` |

Logs: `%AppData%\BalanceBoardApp\logs\session-YYYY-MM-DD.log`

## License

- **This project** (`src/`, `tools/`, `tests/`): [MIT](LICENSE)
- **WiiBalanceWalker** (`reference/WiiBalanceWalker/`): MS-PL — [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)

## Changelog

See [CHANGELOG.md](CHANGELOG.md).

## Acknowledgements

[WiiBalanceWalker](https://github.com/lshachar/WiiBalanceWalker) · [WiimoteLib](https://github.com/lshachar/WiimoteLib) · [vJoy](https://github.com/shauleiz/vJoy)
