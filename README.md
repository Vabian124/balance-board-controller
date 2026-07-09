# Balance Board Controller

[![CI](https://github.com/Vabian124/balance-board-controller/actions/workflows/ci.yml/badge.svg)](https://github.com/Vabian124/balance-board-controller/actions/workflows/ci.yml)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/download)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows%20x64-0078D4?logo=windows)](https://github.com/Vabian124/balance-board-controller)

**v1.5.2 (stable)** — **current main release** and **working connect release** (reference FormBluetooth wake, prepared-board reconnect, stale-pairing SYNC path). A production-quality **.NET 8** desktop app that turns a **Nintendo Wii Fit Balance Board** into a **virtual game controller** ([vJoy](https://github.com/shauleiz/vJoy)) or hand-free keyboard/mouse input.

Clean rewrite of [WiiBalanceWalker v0.5](https://github.com/lshachar/WiiBalanceWalker) with tabbed UI, progressive detail levels, automatic Bluetooth pairing, structured diagnostics, and crash-hardened device handling.

> **Contributors & agents:** [CONTRIBUTING.md](CONTRIBUTING.md) · [INSTRUCTIONS.md](INSTRUCTIONS.md) · [AGENTS.md](AGENTS.md) · [docs/](docs/)

## Features

- **Tabbed UI** — **Dashboard** (connect, balance visual, direction), **Profiles** (presets + sensitivity), **Advanced** (sliders, vJoy, bindings, debug suite)
- **UI detail levels** — **Simple** / **Standard** / **Advanced** progressive disclosure (persisted)
- **Jump presets** — **Easy** / **Normal** / **Hard** for one-foot and lighter users; jump banner on balance visual
- **Game profiles** — Game Controller, **Minecraft (Controlify)**, Pedal/Rudder, Hand-Free Desktop, Balance Mouse
- **Theme** — System / Light / Dark with accessible dropdown contrast in both themes
- **Sensitivity presets** — Low through Highly sensitive; manual deadzone/sensitivity sliders when simple presets are off; response curves in Advanced
- **Smart connect** — first launch pairs on demand; returning users auto-reconnect; **v1.5.2** aligns wake/pair with WiiBalanceWalker FormBluetooth (inline HID wait, prepared-board reconnect, stale-pairing SYNC)
- **Crash-safe lifecycle** — dedicated STA `ConnectionWorker` for WiimoteLib; hardened disconnect (no post-dispose HID callbacks)
- **Structured logs** — `[CONNECT]`, `[DISCONNECT]`, `[JUMP]`, `[VJOY]`, `[SETTINGS]`, `[ERROR]` tags in session log
- **Debug Suite** — health check, session log, copy report
- **Quality gate** — format, Roslyn analyzers, unit/integration/fuzz/automation tests in CI ([`ci.yml`](.github/workflows/ci.yml))

## Requirements

- Windows 10/11 (64-bit)
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) — or SDK if building from source ([`global.json`](global.json))
- [vJoy](https://github.com/shauleiz/vJoy) — reboot after install
- Bluetooth adapter + Wii Fit Balance Board

**Full install guide:** [docs/INSTALL.md](docs/INSTALL.md) (releases, build steps, antivirus notes).

## Quick start

### Option A — Download a release (easiest)

1. Install **vJoy**, reboot, enable **Device 1** (X/Y) in vJoyConf.
2. Install **.NET 8 Desktop Runtime**.
3. Download the latest zip from [GitHub Releases](https://github.com/Vabian124/balance-board-controller/releases) (**[v1.5.2](https://github.com/Vabian124/balance-board-controller/releases/tag/v1.5.2)** — current stable).
4. Extract and run `BalanceBoardApp.exe`.
5. Click **Connect**, press **SYNC** on the board (first time).
6. Stand on the board → **Tare** → verify in vJoy Monitor.

### Option B — Build from source

```powershell
git clone https://github.com/Vabian124/balance-board-controller.git
cd balance-board-controller
.\build.bat
.\start.bat
```

Or double-click **`start.bat`** after a build — it auto-builds on first run.

### First-time UI tips

| Setting | Where | What it does |
|---------|-------|----------------|
| **UI detail** | Profiles tab | Simple hides Advanced tab; Standard adds manual sliders when presets off; Advanced adds triggers, curves, bindings |
| **Jump feel** | Profiles tab (Simple) or Advanced | Easy / Normal / Hard — how much weight must leave the board to jump |
| **Profile** | Profiles tab | Pick a preset; **Minecraft (Controlify)** for modded Minecraft with vJoy |
| **Theme** | Advanced tab (Standard+) | System / Light / Dark |

### Minecraft with Controlify (Modrinth / Fabric)

1. Install [Controlify](https://modrinth.com/mod/controlify) in your Minecraft instance.
2. In this app, select profile **Minecraft (Controlify)** — lean drives vJoy **left stick** (move), one-foot jump sends **vJoy button 1** (gamepad A).
3. In Minecraft: **Options → Controls → Controlify** — bind **vJoy Device 1**; map left stick to walk/strafe and button A to jump.
4. Disable Steam Input if it overrides Controlify on Steam Deck.

### Real hardware vs simulate

| Mode | Command | Use when |
|------|---------|----------|
| **Hardware** | `.\start.bat` or `BalanceBoardApp.exe` | Normal play — pairs over Bluetooth, persists last board ID |
| **Simulate** | `dotnet run … -- --simulate-board --dev` | CI, UI dev, no board — does **not** save `SIM-BOARD-*` to settings |

Sync vJoy DLLs after driver updates: `.\scripts\dev\sync-vjoy-dlls.ps1` then rebuild.

```powershell
dotnet run --project src/BalanceBoard.App/BalanceBoard.App.csproj -c Release -- --simulate-board --dev
```

## Quality & CI

```powershell
.\scripts\lint.ps1              # full gate (delegates to scripts/ci/lint.ps1)
.\scripts\ci\test.ps1           # unified test pipeline (build + all suites)
.\scripts\ci\test.ps1 -Quick    # PR-fast subset (skips fuzz + slow tests)
```

CI runs the same gate on every push/PR ([`.github/workflows/ci.yml`](.github/workflows/ci.yml)).

| Checks | Tool |
|--------|------|
| Format | `dotnet format --verify-no-changes` |
| Static analysis | .NET analyzers + `-warnaserror` (Release) |
| Tests | unit · fuzz · integration · headless WPF UI · automation lifecycle |
| Tools | `Validate` CLI |
| Physical (manual) | `BalanceBoardApp.exe --physical-test connect-basic` |

See [docs/TESTING.md](docs/TESTING.md).

## Repository layout

```
src/
  BalanceBoard.App/     WPF UI (tabbed MainWindow)
  BalanceBoard.Core/    Device logic, processing, vJoy (no WPF)
tests/
  BalanceBoard.*.Tests/ Unit, integration, fuzz, headless WPF UI, automation
  BalanceBoard.Testing/ Shared fakes
  hardware/             Optional board scripts
tools/
  Validate/             CLI health check
scripts/
  ci/                   lint.ps1, test.ps1, verify-tests.ps1
  dev/                  start, stop, restart, connect, sync-vjoy-dlls
  test/                 run-all.ps1 (alias for ci/test.ps1)
reference/              Legacy WiiBalanceWalker (MS-PL, not in solution)
libs/x64/               WiimoteLib + vJoy native DLLs
docs/                   Architecture, CODEMAP, testing guide
```

## Troubleshooting

| Problem | Fix |
|---------|-----|
| vJoy busy | App stops stale feeders; close vJoy Monitor |
| vJoy missing | Reboot after install; check Device Manager |
| Connect / disconnect crash | Update to **v1.5.2 (stable)**; check session log for `[DISCONNECT]` / `[ERROR]` |
| DLL mismatch | Run `.\scripts\dev\sync-vjoy-dlls.ps1` or copy `vJoyInterface*.dll` from vJoy install into `libs/x64/` |
| Dark dropdown unreadable | Fixed in v1.1.1+ — update theme brushes or switch to Light |

Logs: `%AppData%\BalanceBoardApp\logs\session-YYYY-MM-DD.log` — search for structured tags (`[CONNECT]`, `[JUMP]`, etc.).

## Security & antivirus

Open-source MIT project — no network calls at runtime, no admin elevation, no packing/obfuscation. vJoy and optional `SendInput` keyboard mode are standard for gaming/accessibility tools; unsigned builds may trigger SmartScreen. **Build from source** or verify [GitHub Release](https://github.com/Vabian124/balance-board-controller/releases) SHA256 checksums. Details: [docs/INSTALL.md](docs/INSTALL.md#antivirus-and-windows-smartscreen).

## License

- **This project** (`src/`, `tools/`, `tests/`): [MIT](LICENSE)
- **WiiBalanceWalker** (`reference/WiiBalanceWalker/`): MS-PL — [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)

## Changelog

See [CHANGELOG.md](CHANGELOG.md).

## Acknowledgements

[WiiBalanceWalker](https://github.com/lshachar/WiiBalanceWalker) · [WiimoteLib](https://github.com/lshachar/WiimoteLib) · [vJoy](https://github.com/shauleiz/vJoy)
