# Changelog

All notable user-facing changes. For detailed agent work logs see [`docs/updates/`](docs/updates/).

## Unreleased

### Fixed
- Connect button threw `NotImplementedException` (dotnet format switch stubs) — **5bfb026**
- `ObjectDisposedException` on fresh board connect — **ConnectionWorker** STA thread — **aebfc01**

### Added
- `ConnectionWorker` — single STA thread for WiimoteLib/Bluetooth (fixes connect crash)
- Test pyramid: unit, integration, fuzz, automation (`--simulate-board`), hardware scripts
- `scripts/ci/` quality gate with format, analyzers, and lifecycle smoke
- `ConnectResult` structured connect outcomes; crash-hardened error paths

### Changed
- Poll loop moved off thread pool onto `ConnectionWorker`
- UI uses `Dispatcher.BeginInvoke` to avoid connect deadlocks
- Professional repo layout: `scripts/dev/`, `scripts/ci/`, `docs/testing/`

### Removed
- `StaThread` (superseded by `ConnectionWorker`)

## 2026-07-07 — Repo cleanup & connection hardening

- Connection flow logging, global exception logging, multi-HID scan
- Python port prep (`BalanceMath`, abstractions, unit tests)
- Legacy code under `reference/`; canonical DLLs in `libs/x64/`

## Earlier

- Modern WPF dashboard, automatic Bluetooth pairing, vJoy integration
- Rewrite of WiiBalanceWalker v0.5
