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

## [1.1.1] - 2026-07-07

Stability and accessibility release: crash-proof disconnect, Minecraft preset, jump tuning, progressive UI.

### Added
- **UI detail levels** — Simple / Standard / Advanced progressive disclosure (persisted in settings)
- **Jump presets** — Easy / Normal / Hard (`JumpPresets`) for one-foot and lighter users
- Top-center **jump banner** on balance visual plus prominent "Jump!" direction text
- **Minecraft (Controlify)** profile with safe theme brush resolution
- Structured log prefixes: `[CONNECT]`, `[DISCONNECT]`, `[JUMP]`, `[VJOY]`, `[SETTINGS]`, `[ERROR]`
- `BalanceDisplay` helper shared by WPF visual and unit tests
- `scripts/dev/sync-vjoy-dlls.ps1` — copy `vJoyInterface*.dll` from vJoy install into `libs/x64/`
- Connect success sound (Windows notification chime) for real hardware sessions
- UiSmoke applies Minecraft preset; integration tests for disconnect and late HID callbacks

### Fixed
- **Minecraft preset crash** — `ResourceReferenceKeyNotFoundException` / invalid brush cast in profile styling
- **Disconnect fatals** — `ObjectDisposedException` from WiimoteLib `OnReadData` after HID dispose
- Connect crash after Bluetooth pairing (unsafe HID wake probe + collection not released)
- Simulated board IDs (`SIM-BOARD-*`) no longer pollute `LastConnectedDeviceId` after `--simulate-board`
- Redundant vJoy re-acquire spam when toggling settings or applying presets
- Dev mode (`--dev`) no longer terminates sibling `BalanceBoardApp` instances during vJoy cleanup

## Unreleased

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
