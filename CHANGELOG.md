# Changelog

All notable user-facing changes. For detailed agent work logs see [`docs/updates/`](docs/updates/).

## [1.1.0] - 2026-07-07

Feature release: accessibility, mouse mode, dark theme, and balance-visual fixes.

### Added
- **Sensitivity presets** — Low / Medium / High / Highly sensitive (simple mode) plus advanced sliders for fine tuning
- **Balance Mouse** profile — lean to move cursor, jump for left-click
- **Dark theme** — System / Light / Dark toggle (persisted in settings)
- **Keyboard & mouse binding editor** — per-slot key, mouse button, and mouse-move configuration in the UI
- Configurable **jump threshold** and **jump hold** duration in advanced sensitivity settings
- `SensitivityPresets`, `ThemeManager`, `Colors.Light.xaml` / `Colors.Dark.xaml` theme palettes
- Centralized `DeviceIdRules.ExtractFromHidPath` (code-review quick win)
- vJoy axis write coalescing (skip unchanged axes)

### Fixed
- Live balance dot stuck **top-left** when idle — centers at 50% when weight is below threshold
- **vJoy drift** when board empty — neutral axes (0) and centered balance after tare
- **Profile switch UI** — preset buttons highlight the active profile; startup no longer forces Game Controller on every launch
- **Jump detection** works during brief lift-off; desktop/mouse profiles bind jump to **mouse click**
- Center dot color changed to rose/pink for visibility on light and dark themes

### Changed
- Desktop preset jump action: Space → **left mouse click**
- `RunDeferredStartup` only applies default game profile on **first launch**

## Unreleased

### Fixed
- Connect crash after Bluetooth pairing — `ObjectDisposedException` in WiimoteLib `OnReadData` (unsafe HID wake probe + collection not released)
- Simulated board IDs (`SIM-BOARD-*`) no longer pollute `LastConnectedDeviceId` / session log "Last board" after `--simulate-board` runs
- Redundant vJoy re-acquire spam when toggling settings or applying presets (same device already held)
- Dev mode (`--dev`) no longer terminates sibling `BalanceBoardApp` instances during vJoy cleanup

### Added
- `scripts/dev/sync-vjoy-dlls.ps1` — copy `vJoyInterface*.dll` from vJoy install into `libs/x64/`
- Connect success sound (Windows notification chime) for real hardware sessions
- Regression test: connect flow logs `[CONNECT] First balance reading`

## [1.0.0] - 2026-07-07

First production-ready release for public use.

### Fixed
- Connect button threw `NotImplementedException` (dotnet format switch stubs) — **5bfb026**
- `ObjectDisposedException` on fresh board connect — **ConnectionWorker** STA thread — **aebfc01**

### Added
- `ConnectionWorker` — single STA thread for WiimoteLib/Bluetooth (fixes connect crash)
- Test pyramid: unit, integration, fuzz, automation (`--simulate-board`), hardware scripts
- `scripts/ci/` quality gate with format, analyzers, crash-safety grep, lifecycle smoke
- `ConnectResult` structured connect outcomes; crash-hardened error paths
- `build.bat`, `docs/INSTALL.md`, GitHub Release workflow, assembly metadata, `asInvoker` manifest

### Changed
- Poll loop moved off thread pool onto `ConnectionWorker`
- UI uses `Dispatcher.BeginInvoke` to avoid connect deadlocks
- Professional repo layout: `scripts/dev/`, `scripts/ci/`, `docs/testing/`
- `start.bat` runs production mode (single instance); use `scripts/dev/start.ps1` for `--dev`

### Removed
- `StaThread` (superseded by `ConnectionWorker`)

## 2026-07-07 — Repo cleanup & connection hardening

- Connection flow logging, global exception logging, multi-HID scan
- Python port prep (`BalanceMath`, abstractions, unit tests)
- Legacy code under `reference/`; canonical DLLs in `libs/x64/`

## Earlier

- Modern WPF dashboard, automatic Bluetooth pairing, vJoy integration
- Rewrite of WiiBalanceWalker v0.5
