# Coding standards (draft)

> Merge into `CONTRIBUTING.md` or `docs/STANDARDS.md` after team review.  
> Status: **Proposed** — not yet enforced beyond existing CI.

## Layering

### Do

- Put device, math, vJoy, and input logic in `BalanceBoard.Core`.
- Put WPF XAML, theming, and `Dispatcher` marshaling in `BalanceBoard.App`.
- Route all WiimoteLib and Bluetooth operations through `ConnectionWorker` (STA thread).
- Use `ActionPresets.Apply*` for output profiles — never duplicate binding tables in UI.
- Inject fakes via `IBalanceBoardConnection`, `IGameControllerOutput`, `IActionSimulator` in tests.
- Keep `BalanceBoardSession` as the single orchestrator for connect → poll → process → output.

### Don't

- Reference WPF, WinForms, or `System.Windows` from Core.
- Call `WiimoteLib` from UI thread, thread pool, or `async` continuations without worker marshaling.
- Add a second poll loop or timer that processes balance readings outside `BalanceBoardSession`.
- Duplicate HID path parsing — use `DeviceIdRules.ExtractFromHidPath` (once centralized per R-02).
- Put business logic in XAML code-behind beyond UI binding and `Dispatcher` calls.

## Naming

| Pattern | Example |
|---------|---------|
| File-scoped namespace | `namespace BalanceBoard.Core.Services;` |
| Interfaces | `I` prefix: `IBalanceBoardConnection` |
| Private fields | `_camelCase` |
| Constants | `BalanceConstants`, `TriggerDefaults`, `ActionSlots` |
| Connect trace prefix | `[CONNECT]` in log lines for pairing/HID |
| Review item IDs | `R-01`, `P-01`, `W-04`, `T-01` in `docs/suggestions/` |

## Settings

### Do

- Load settings before `InitializeComponent()` in `MainWindow` constructor.
- Guard programmatic UI updates with `_suppressSettingEvents = true`.
- Gate saves with `_uiReady`.
- Persist via `SettingsStore.Save` after mutating `AppSettings`.
- Call `_session.LoadSettings` after save so vJoy syncs.
- Use `DeviceIdRules.ShouldPersistConnectionState` before writing `LastDeviceId`.

### Don't

- Save settings on every slider tick without debounce if performance becomes an issue (currently acceptable at 20 Hz max).
- Mutate `_settings` in Core without going through `SettingsStore` for persistence.
- Store simulated device IDs in persisted settings (see `DeviceIdRules.IsSimulated`).

## Events and threading

### Do

- Use `Dispatcher.BeginInvoke` for session → UI updates (non-blocking).
- Wrap subscriber callbacks with `SafeCallbacks.Raise` from worker/timer threads.
- Swallow exceptions in `Dispose` paths — teardown must not throw.
- Use `CancellationToken` for connect flows; link UI cancel to `_session.CancelConnect()`.
- Marshal all connect/disconnect/pair calls through `_worker.Invoke` or `_worker.InvokeAsync`.

### Don't

- Use `Dispatcher.Invoke` from high-frequency `Processed` handlers unless required.
- Let subscriber exceptions propagate from `ConnectionWorker` loop.
- Call `Environment.Exit` or `Shutdown(-1)` in product code (CI `check-crash-safety.ps1` forbids).
- Call `Poll()` directly from WiimoteLib callbacks without a re-entrancy strategy (see R-01).

## Poll model (pending R-01 decision)

Until R-01 is resolved, treat the dual path as a **known defect**, not a pattern to copy.

### Do (after R-01)

- Document the chosen model in `ARCHITECTURE.md` and AGENTS.md.
- If **poll-only:** remove `ReadingAvailable` subscription; worker tick is sole driver.
- If **event-only:** disable worker poll while connected; keep tick as watchdog only.
- Add a re-entrancy guard or lock around `OnReading` if both paths can coexist briefly.

### Don't

- Add a third timer or background task that also calls `Poll()`.
- Assume `ARCHITECTURE.md` "polling only" is accurate — verify against `BalanceBoardSession` constructor.

## vJoy

### Do

- Run `FeederProcessCleanup` before acquire when status is `VJD_STAT_BUSY`.
- `RelinquishVJD` on shutdown; center axes first.
- Respect `EnableVJoy`, `SendCenterOfGravityToAxes`, `SendLoadSensorsToAxes` in output path.
- Early-return from `Initialize` if the same device is already acquired (avoids re-acquire spam).
- After P-01 lands: cache last axis values and skip `SetAxis` when unchanged.

### Don't

- Acquire vJoy from multiple threads without synchronization.
- Skip feeder cleanup on startup (unless `--no-cleanup` dev flag).
- Call `SetAxis` for all six axes on every poll when values are unchanged.
- Write axes the user disabled via `SendCenterOfGravityToAxes` / `SendLoadSensorsToAxes`.

## Wiimote / Bluetooth

### Do

- Use `WiimoteCollectionHelper.ReleaseAll` before collections go out of scope.
- Honor `BalanceConstants.HidCallbackDrainMs` after disconnect.
- Unsubscribe `WiimoteChanged` before `Disconnect`.
- Use `ConnectionIntent.QuickReconnect` for returning users; `PairAndConnect` only on explicit user action or `--connect`.
- Short-circuit `DiscoverDeviceIds` when already connected (return current device ID only).
- Run `WakePairedDevices` before `QuickReconnect` HID connect.

### Don't

- Probe HID with new `WiimoteCollection` while connected device is active (except controlled wake in pairing).
- Shorten disconnect grace sleeps without hardware regression tests.
- Call `PairAndConnect` on every auto-connect — use `QuickReconnect` for `AutoConnectOnStartup`.
- Assume BT drop will auto-recover — W-04 is not implemented yet.

### Connection intent reference

| Intent | When | Pairing? |
|--------|------|----------|
| `QuickReconnect` | Auto-connect, second instance, `--simulate-board` | No — wake + HID only |
| `PairAndConnect` | Connect button, `--connect` launch flag | Yes — full discovery |

## Logging

### Do

- Use `FileLogService` / `session.Log` for user-visible diagnostics.
- Write exceptions via `WriteException(context)`.
- Log connect flow milestones (`ConnectionFlowLogger`).
- Prefix HID/BT trace lines with `[CONNECT]` for grep-friendly logs.

### Don't

- Commit `%AppData%` logs, `*.log`, or session files from dev machines.
- Log stack traces to UI without also writing to file log.
- Log at `Error` level for expected disconnect paths during reconnect attempts.

## Tests

### Do

- Add golden tests for `BalanceMath` / `BalanceProcessor` behavior changes.
- Use `BalanceBoard.Testing` fakes for integration tests.
- Run `.\scripts\ci\lint.ps1` (or `.\scripts\lint.ps1`) before PR.
- Raise `ReadingAvailable` from fakes when testing event-driven paths (R-05).
- Add dual-path regression test after R-01 fix (T-01).

### Don't

- Add tests that require vJoy or Bluetooth in default CI (use fakes).
- Assert trivial truths (e.g. `Assert.True(true)`).
- Rely on fakes that never fire `ReadingAvailable` to validate poll/event interaction.

## Documentation

### Do

- Update `docs/CODEMAP.md` when adding/removing files.
- Update `docs/ARCHITECTURE.md` when data flow changes.
- Add `docs/updates/` entry for meaningful commits.
- Mark `docs/ROADMAP.md` items when completed.
- Check `docs/suggestions/` before large refactors.

### Don't

- Leave cursor rules (`wpf-ui.mdc`) referencing deleted windows/files.
- Reference `build.yml` — workflow is `.github/workflows/ci.yml`.
- Document poll model before R-01 is decided and implemented.

## Commits (draft — reconcile with INSTRUCTIONS)

### Proposed default

- Commit when the user asks, or when a logical unit of work is complete and the user expects delivery.
- AI agents: read `INSTRUCTIONS.md` each pass; follow user override for "do not commit."
- When implementing a suggestion, reference the item ID (e.g. `R-01`) in commit message or PR description.

### Don't

- Amend pushed commits unless user explicitly requests.
- Commit secrets, logs, or `%AppData%` artifacts.

## Python port (when applicable)

### Do

- Port `Processing/` verbatim first; run same golden vectors as `BalanceProcessorTests`.
- Keep `settings.json` keys compatible; add `SchemaVersion` before breaking changes.
- Windows-only code stays in `adapters/` — never import in pure modules.
- Document poll vs event contract in `PYTHON_PORTING.md` after R-01 (PY-03).

### Don't

- Port `ConnectionWorker` / WiimoteLib threading model literally — plan a Python adapter.
- Break settings JSON without a migration in `SettingsStore.ApplyMigrations`.
- Rename `ProcessedBalance.Joy*` to `Sensor*` until port work starts (PY-04).
