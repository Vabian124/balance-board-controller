# Balance Board Controller

[![CI](https://github.com/Vabian124/balance-board-controller/actions/workflows/ci.yml/badge.svg)](https://github.com/Vabian124/balance-board-controller/actions/workflows/ci.yml)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/download)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows%20x64-0078D4?logo=windows)](https://github.com/Vabian124/balance-board-controller)

A production-quality **.NET 8** desktop app that turns a **Nintendo Wii Fit Balance Board** into a **virtual game controller** ([vJoy](https://github.com/shauleiz/vJoy)) or hand-free keyboard/mouse input.

Clean rewrite of [WiiBalanceWalker v0.5](https://github.com/lshachar/WiiBalanceWalker) with automatic Bluetooth pairing, structured connect flows, diagnostics, fuzz/integration tests, and crash-hardened device handling.

> **Contributors & agents:** [CONTRIBUTING.md](CONTRIBUTING.md) · [INSTRUCTIONS.md](INSTRUCTIONS.md) · [AGENTS.md](AGENTS.md) · [docs/](docs/)

## Features

- **Live dashboard** — Wii Fit–style balance visual, direction text, connection status
- **Game controller mode** — lean → vJoy X/Y (works with most joystick games)
- **Presets** — game controller, **Minecraft (Controlify)**, pedals/rudder, hand-free WASD desktop, balance mouse
- **In-app control mapping** — key / mouse bindings for all 8 action slots
- **Dark mode** — follows Windows or force light/dark
- **Sensitivity presets** — Low through Highly sensitive for kids and accessibility
- **Smart connect** — first launch pairs on demand; returning users auto-reconnect
- **Crash-safe connect** — dedicated STA `ConnectionWorker` for WiimoteLib (no thread-pool dispose races)
- **Debug Suite** — health check, session log, copy report
- **Quality gate** — format, Roslyn analyzers, unit/integration/fuzz/automation tests in CI

## Requirements

- Windows 10/11 (64-bit)
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) — or SDK if building from source ([`global.json`](global.json))
- [vJoy](https://github.com/shauleiz/vJoy) — reboot after install
- Bluetooth adapter + Wii Fit Balance Board

**Full install guide:** [docs/INSTALL.md](docs/INSTALL.md) (build steps, releases, antivirus notes).

## Quick start

1. Install **vJoy**, reboot, enable **Device 1** (X/Y) in vJoyConf.
2. Install **.NET 8 Desktop Runtime** (or SDK).
3. Clone, build, run:

```powershell
git clone https://github.com/Vabian124/balance-board-controller.git
cd balance-board-controller
.\build.bat
.\start.bat
```

Or double-click **`start.bat`** after a build — it auto-builds on first run.

4. Click **Connect**, press **SYNC** on the board (first time).
5. Stand on the board → **Tare** → verify in vJoy Monitor.

### Minecraft with Controlify (Modrinth / Fabric)

1. Install [Controlify](https://modrinth.com/mod/controlify) in your Minecraft instance (launcher choice does not matter).
2. In this app, select profile **Minecraft (Controlify)** — lean drives vJoy **left stick** (move), one-foot jump sends **vJoy button 1** (gamepad A).
3. In Minecraft: **Options → Controls → Controlify** — bind **vJoy Device 1**; map left stick to walk/strafe and button A to jump. Use mouse or right stick for look.
4. Disable Steam Input if it overrides Controlify on Steam Deck.

<!-- Screenshots: add docs/images/dashboard.png and docs/images/connect.png when available -->

### Real hardware vs simulate

| Mode | Command | Use when |
|------|---------|----------|
| **Hardware** | `.\start.bat` or `BalanceBoardApp.exe` | Normal play — pairs over Bluetooth, persists last board ID |
| **Simulate** | `dotnet run … -- --simulate-board --dev` | CI, UI dev, no board — does **not** save `SIM-BOARD-*` to settings |

Sync vJoy DLLs after driver updates: `.\scripts\dev\sync-vjoy-dlls.ps1` then rebuild.

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
| DLL mismatch | Run `.\scripts\dev\sync-vjoy-dlls.ps1` or copy `vJoyInterface*.dll` from your vJoy install into `libs/x64/` |

Logs: `%AppData%\BalanceBoardApp\logs\session-YYYY-MM-DD.log`

## Security & antivirus

Open-source MIT project — no network calls at runtime, no admin elevation, no packing/obfuscation. vJoy and optional `SendInput` keyboard mode are standard for gaming/accessibility tools; unsigned builds may trigger SmartScreen. **Build from source** or verify [GitHub Release](https://github.com/Vabian124/balance-board-controller/releases) SHA256 checksums. Details: [docs/INSTALL.md](docs/INSTALL.md#antivirus-and-windows-smartscreen).

## License

- **This project** (`src/`, `tools/`, `tests/`): [MIT](LICENSE)
- **WiiBalanceWalker** (`reference/WiiBalanceWalker/`): MS-PL — [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)

## Changelog

See [CHANGELOG.md](CHANGELOG.md).

## Acknowledgements

[WiiBalanceWalker](https://github.com/lshachar/WiiBalanceWalker) · [WiimoteLib](https://github.com/lshachar/WiimoteLib) · [vJoy](https://github.com/shauleiz/vJoy)
