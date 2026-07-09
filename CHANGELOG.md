# Changelog

All notable user-facing changes. For detailed agent work logs see [`docs/updates/`](docs/updates/).


## [1.5.2] - 2026-07-09

Connect flow alignment with WiiBalanceWalker FormBluetooth and scripted reference test fixtures.

### Added
- **ScriptedBluetoothPairingService** + **ReferenceConnectScenario** — fake Bluetooth flows for all six WiiBalanceWalker connect scenarios (FormMain fast path, fresh SYNC pair, stale Windows pairing, deleted pairing, board on feet, board off)
- **WiiBalanceWalkerConnectFlowTests** — integration coverage for every reference scenario plus no-remembered escalation, stale-pairing removal, and dynamic HID-after-wake
- **WiiBluetoothPinTests** — permanent host-MAC PIN matches reference FormBluetooth

### Changed
- **PairDiscoverableBoard** — after successful pair, inline FormBluetooth 4s HID wait + wake ping (no duplicate wake from callers)
- Assembly and file version bumped to **1.5.2**

## [1.5.1] - 2026-07-09

Phase 2 structural refactor: Core output pipeline, SettingsSync, tab views, and test split.

### Added
- **FrameOutput** and **OutputRoutingPolicy** — extracted from `BalanceBoardSession` for cleaner vJoy vs keyboard routing
- **ProfileCoordinator** — preset/vJoy sync marshalled onto the ConnectionWorker thread
- **BalanceReadoutText** / **ConnectionStatusText** — dashboard readout formatting moved to Core
- **Tab UserControls** — `DashboardView`, `ProfilesView`, `FineTuningView`, `AdvancedView` plus `ConnectionToolbar` and `SessionLogPanel`
- **SettingsSync** — single map between `AppSettings` and MainWindow controls with debounced saves
- **GlobalUsings** — namespace imports for reorganized Core service folders
- Split Core tests into `Models/` and `Processing/` suites; added `OutputMode` and `FrameOutput` coverage

### Changed
- Core `Services/` reorganized into domain folders (`Connection/`, `Diagnostics/`, `Output/`, `Session/`, `Settings/`)
- Namespaces aligned with folder structure for CI format gate
- Assembly and file version bumped to **1.5.1**

## [1.5.0] - 2026-07-08

Output-mode refactor: mutually exclusive keyboard vs vJoy movement, Minecraft keyboard default, board-button mapping.

### Added
- **Output mode** — pick keyboard/mouse or vJoy as primary movement output (prevents double input)
- **Minecraft (Controlify)** profile — separate from keyboard-first **Minecraft** (WASD + Space)
- **Board button slot** — map power/A to a key or vJoy button independently of movement
- **Configurable jump vJoy button** — choose button 1–32 when jump maps to vJoy
- **vJoy device picker** — lists configured devices with axis/button summary
- **Movement hysteresis** — reduces trigger-edge flicker on cardinal directions
- Settings migration for `OutputMode` and `JumpVJoyButton` from older saves

### Changed
- First-run default is **Minecraft** keyboard (WASD + Space), not vJoy Controlify
- `ActionEngine` processes movement slots separately from board-button key bindings
- `VJoyController` tracks multiple buttons; jump no longer forces board A onto vJoy button 1
- Assembly and file version bumped to **1.5.0**

## [1.4.2] - 2026-07-08

User-reported bug fixes from PR #13 (weight display, keyboard, mouse, autoconnect, vJoy UX).

### Added
- **vJoy Configure** button with `VJoyConfigLocator` when the driver is installed but the device is not configured
- **`docs/DEVELOPER_API.md`** for integrators

### Fixed
- **Weight display** - absolute weight after Wiimote tare no longer drifts incorrectly
- **Forward=W keyboard** regression restored for desktop preset
- **Balance Mouse** - proportional movement instead of fixed step size
- **Startup auto-reconnect** recovery when a saved board is paired but not yet connected
- Expanded regression tests for the above

### Changed
- Assembly and file version bumped to **1.4.2**
## [1.4.1] - 2026-07-08

Custom profiles, multi-device picker, accessibility, and a broad hardening + concurrency pass.

### Added
- **Custom named profiles** — save / load / update / delete plus export / import via `NamePromptDialog`
- **Multi-device picker** — choose among several paired balance boards at connect time
- **Configurable poll rate** — `ConnectionWorker.PollIntervalMs` exposed in settings
- **Reset to defaults** and honor **Start minimized** on launch
- **Accessibility** — live regions for connection/status announcements
- Input-simulation end-to-end tests and concurrent-poll regression tests

### Fixed
- **Poll reentrancy** — `_pollGate` skips overlapping `Poll()` between the worker tick and HID `ReadingAvailable`
- **Connect vs. device-picker race** — engage `_connectInProgress` before the picker `ShowDialog`
- **Sticky keys** — `ActionEngine` releases held keys on rebind; `KEYEVENTF_EXTENDEDKEY` set for extended keys
- **Binding erase** — clearing an action slot no longer leaves a stale key held
- **BalanceMath axis clamp** — joystick axis values clamped to valid vJoy range
- **Thread safety** — `SettingsStore` IO lock and `VJoyController` sync lock; feeder path existence check before acquire
- **DebugSessionTrace** made a no-op that no longer embeds a personal filesystem path
- **CI format gate** — resolve `IDE0047` / `IDE0305` style violations in test projects so the quality gate stays green

### Changed
- Dependency updates: `Microsoft.NET.Test.Sdk` 18.7.0, `xunit` 2.9.3 (#9, #10)
- Assembly and file version bumped to **1.4.1**

## [1.4.0] - 2026-07-08

Unified automated test pipeline, hang hardening, and opt-in physical hardware validation.

### Added
- **Unified test pipeline** — `scripts/ci/test.ps1` runs build + unit, integration, headless WPF UI, Validate CLI, and lifecycle suites with structured `artifacts/test/` logs and TRX output
- **Headless WPF UI tests** — `BalanceBoard.App.Ui.Tests` covers navigation, profiles, settings persistence, simulated connect, and shutdown without user interaction
- **Physical test lane** — `--physical-test connect-basic` guided manual hardware validation with per-run artifacts under `%AppData%\BalanceBoardApp\artifacts\physical-tests\`
- **Process test harness** — lifecycle automation uses timeouts and tree-kill cleanup for spawned `BalanceBoardApp` processes
- **`docs/TESTING.md`** — testing guide for automated vs physical lanes

### Fixed
- **`--auto-exit-after` without connect** — lifecycle smoke exits on schedule even when no board connect runs
- **ConnectionWorker invoke timeouts** — queued actions abort after 15s instead of hanging callers indefinitely
- **WPF test host dispatcher timeouts** — UI test harness fails fast on stuck dispatcher work
- **Test pipeline watchdogs** — per-suite command timeouts and stale `testhost` / `BalanceBoardApp` cleanup between suites
- **Simulated connect on CI** — skip Bluetooth radio wait for `SimulatedBalanceBoardConnection` (fixes UI + lifecycle timeouts)
- **Fast release path** — release workflow packages only after green CI; `scripts/release/quick-release.ps1` for maintainers

## [1.3.0] - 2026-07-07

vJoy stick center mapping and per-axis tuning.

### Added
- **Per-axis deadzone and sensitivity** — optional split tuning for left/right vs forward/back on Fine Tuning tab
- `VJoyAxisMapping` — maps signed stick values (0 = center) to each vJoy device's axis min/max range

### Fixed
- **Idle stick at top-left** — unsigned vJoy configs (0–32767) now center at ~16384 instead of sending 0
- **Nullable deadzone settings** — per-axis deadzone uses `double?` in settings JSON when split mode is off

## [1.2.4] - 2026-07-07

Connect reliability, Fine Tuning tab, and hair-trigger stick sensitivity.

### Added
- **Fine Tuning tab** — sensitivity/jump presets and sliders between Profiles and Advanced
- **Hair trigger** sensitivity preset (~2.5% lean → full vJoy stick); slider range extended to 25×
- **Windows HID instance ID** parsing (`37A15347` instead of product id `0306`)
- Debug session trace (`DebugSessionTrace`) for connect-flow diagnostics

### Fixed
- **False “Bluetooth off”** — trust readable adapter MAC when InTheHand reports stale `PowerOff`
- **Post-SYNC crash** — minimal wake probe (LED only, no continuous 0x34) after pairing
- **Connect button** — always `PairAndConnect` on manual Connect; UI deadlock removed from `StatusChanged`
- **Stick mapping** — sensitivity now controls how little lean is needed for full throw

## [1.2.3] - 2026-07-07

Wake paired boards without SYNC: Bluetooth reconnect before HID ping, crash-safe HID gate, and smarter Connect button.

### Fixed
- **Paired board blinking** — wake probe runs Bluetooth inquiry + HID retries when the board is not yet in the Windows HID list (no ping was sent before)
- **Connect on power-on** — serialized `HidGate` prevents WiimoteLib `OnReadData` / `ThreadPoolBoundHandle` fatals during wake/connect races
- **Returning users** — Connect button uses `QuickReconnect` (wake-first) when `HasConnectedBefore` is set
- **Post-pair HID boot** — retry HID open up to 4 times while the board finishes waking after Bluetooth pairing

## [1.2.2] - 2026-07-07

Stability and accessibility release: crash-proof disconnect, Minecraft preset, jump tuning, progressive UI.

### Added
- **UI detail levels** — Simple / Standard / Advanced progressive disclosure (persisted in settings)
- **Jump presets** — Easy / Normal / Hard (`JumpPresets`) for one-foot and lighter users
- Top-center **jump banner** on balance visual plus prominent "Jump!" direction text
- **Minecraft (Controlify)** profile with safe theme brush resolution
- **Tabbed UI** — Dashboard / Profiles / Advanced
- Structured log prefixes: `[CONNECT]`, `[DISCONNECT]`, `[JUMP]`, `[VJOY]`, `[SETTINGS]`, `[ERROR]`
- `BalanceDisplay` helper shared by WPF visual and unit tests
- `scripts/dev/sync-vjoy-dlls.ps1` — copy `vJoyInterface*.dll` from vJoy install into `libs/x64/`
- Connect success sound (Windows notification chime) for real hardware sessions
- UiSmoke applies Minecraft preset; integration tests for disconnect and late HID callbacks

### Fixed
- **Minecraft preset crash** — `ResourceReferenceKeyNotFoundException` / invalid brush cast in profile styling
- **Disconnect fatals** — `ObjectDisposedException` from WiimoteLib `OnReadData` after HID dispose
- **Dark-mode ComboBox** — readable dropdown contrast in light and dark themes
- Connect crash after Bluetooth pairing (unsafe HID wake probe + collection not released)
- Simulated board IDs (`SIM-BOARD-*`) no longer pollute `LastConnectedDeviceId` after `--simulate-board`
- Redundant vJoy re-acquire spam when toggling settings or applying presets
- Dev mode (`--dev`) no longer terminates sibling `BalanceBoardApp` instances during vJoy cleanup


## [1.2.2] - 2026-07-07

Connect reliability: safer pairing/HID handoff, Bluetooth-off handling, and duplicate-connect guard.

### Fixed
- **Connect before board is on** — pairing failure returns cleanly without crashing when no Nintendo device is found
- **Duplicate Connect clicks** — second request returns `AlreadyInProgress` instead of overlapping flows
- **Bluetooth off** — connect waits for the radio when possible; clear status when BT stays unavailable
- **Post-pair wake** — skip redundant wake probe immediately after successful pairing
- **HID discovery** — handle `WiimoteNotFoundException` during discovery without fatal errors
- **Wake probe teardown** — longer callback drain after wake disconnect to avoid late `OnReadData` crashes
## [1.2.1] - 2026-07-07

Sensitivity UX fix and per-profile response curves.

### Added
- **Response curves** — Linear, Ease in/out, Exponential, and Minecraft snappy presets (Advanced tab); persisted in settings
- **One-foot mode** toggle — applies highly sensitive + easy jump + ease-in/out curve in one click
- **Standard-tier sliders** — deadzone, sensitivity, invert, and jump threshold on Profiles when simple presets are off

### Fixed
- **Simple presets unchecked** no longer hides all sliders — manual tuning appears on Profiles without requiring Advanced detail level

## [1.2.0] - 2026-07-07

Production release: correct version metadata and release asset naming.

### Fixed
- **Release zip naming** — `Directory.Build.props` version now matches Git tags and published assets (`BalanceBoardController-1.2.0-win-x64.zip`); v1.1.1 shipped a misnamed `1.1.0` zip

### Changed
- Assembly and file version bumped to **1.2.0** across all projects
- Dependency updates: `Microsoft.NET.Test.Sdk` 18.7.0, `xunit` 2.9.3, `xunit.runner.visualstudio` 3.1.5, `softprops/action-gh-release` v3, `Microsoft.CodeAnalysis.NetAnalyzers` 10.0.301

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
