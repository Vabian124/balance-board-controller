# Code review — 2026-07-07

Architecture and standards review of Balance Board Controller (.NET 8 WPF).

Compared against: `reference/WiiBalanceWalker/` (v0.5), `AGENTS.md`, `docs/ARCHITECTURE.md`, `docs/CODEMAP.md`, `INSTRUCTIONS.md`, `CONTRIBUTING.md`.

Sampled: `BalanceBoardSession`, `ConnectionWorker`, `MainWindow`, `BalanceProcessor`, `BluetoothPairingService`, `WiimoteCollectionHelper`, `BalanceBoardConnection`, `VJoyController`.

---

## Executive summary — top 10 priorities

| # | Item | Priority | Effort |
|---|------|----------|--------|
| 1 | Resolve dual poll path (worker tick + `ReadingAvailable`) | **P0** | S |
| 2 | Sync stale architecture / cursor rules / roadmap | **P0** | S |
| 3 | Centralize HID `ExtractDeviceId` | **P1** | S |
| 4 | vJoy axis write coalescing | **P1** | S |
| 5 | Graceful Bluetooth disconnect + auto-reconnect | **P1** | L |
| 6 | Split `MainWindow.xaml.cs` responsibilities | **P2** | M |
| 7 | Close test gaps (session, vJoy, settings migrations, Wiimote lifecycle) | **P2** | M |
| 8 | Reconcile LLM commit policy (INSTRUCTIONS vs user preference) | **P2** | S |
| 9 | Strengthen CI (coverage, meaningful static-analysis job) | **P3** | M |
| 10 | Python port: document poll vs event contract | **P2** | S |

---

## Conventions

### C-01 — File-scoped namespaces and nullable

**Status:** Proposed  
**Priority:** P3 | **Effort:** S | **Risk:** Low  
**Suggested action:** Already consistent across `src/`. Enforce via `dotnet format` (CI). No change needed unless new files regress.

### C-02 — `ConnectionWorker` as sole Wiimote thread

**Status:** Proposed (partially implemented)  
**Priority:** P0 | **Effort:** S | **Risk:** High if violated  
**Suggested action:** All `IBalanceBoardConnection` / `IBluetoothPairingService` calls must go through `_worker.Invoke*` from `BalanceBoardSession`. Document in AGENTS.md. Never call WiimoteLib from UI thread or thread pool.

### C-03 — Settings mutation pattern

**Status:** Proposed  
**Priority:** P2 | **Effort:** S | **Risk:** Medium  
**Suggested action:** Standardize: mutate `_settings` → `SettingsStore.Save` → `_session.LoadSettings`. UI already does this in `SaveSettingsFromUi`; audit preset handlers for paths that skip save.

### C-04 — Event naming: `Log` vs `ConnectLog` vs `StatusChanged`

**Status:** Proposed  
**Priority:** P3 | **Effort:** S | **Risk:** Low  
**Suggested action:** Document semantics: `ConnectLog` = HID/BT trace, `Log` = session-level, `StatusChanged` = user-facing one-liner. Avoid routing all three to the same UI handler without prefixes.

### C-05 — Magic numbers → `BalanceConstants`

**Status:** Proposed (mostly done)  
**Priority:** P3 | **Effort:** S | **Risk:** Low  
**Suggested action:** Audit `MainWindow` (`WeightKg < 5` in `DescribeDirection`) — use `BalanceConstants.WeightOnBoardThresholdKg`.

### C-06 — Dispatcher: prefer `BeginInvoke` over `Invoke`

**Status:** Proposed  
**Priority:** P2 | **Effort:** S | **Risk:** Low  
**Suggested action:** `MainWindow` correctly uses `BeginInvoke`. Update `.cursor/rules/wpf-ui.mdc` (currently says `Invoke`).

### C-07 — LLM commit policy conflict

**Status:** Proposed  
**Priority:** P2 | **Effort:** S | **Risk:** Low (process)  
**Suggested action:** `INSTRUCTIONS.md` and `.cursor/rules/project-core.mdc` say "commit every change"; user-facing Cursor rules say "commit only when asked." Pick one default; document exception in INSTRUCTIONS.

---

## Refactoring

### R-01 — Dual poll path in `BalanceBoardSession`

**Status:** Proposed (likely in flight with vJoy spam fix)  
**Priority:** P0 | **Effort:** S | **Risk:** High  
**Paths:** `BalanceBoardSession.cs`, `BalanceBoardConnection.cs`, `ConnectionWorker.cs`  
**Issue:** `Poll()` runs from (a) `ConnectionWorker` 50 ms tick via `SetPollTick` and (b) `OnReadingAvailable` via `WiimoteChanged`. `SetReportType(..., true)` increases event frequency. Likely doubles vJoy/input writes. Worker tick runs on STA worker thread; `OnWiimoteChanged` runs on WiimoteLib callback thread — both can call `Poll()` concurrently with no re-entrancy guard on `OnReading`.  
**Suggested action:** Choose one strategy:
- **Event-only:** disable worker poll while connected; keep tick as watchdog fallback.
- **Poll-only:** remove `ReadingAvailable` subscription (matches current ARCHITECTURE.md).
Add debounce or re-entrancy guard so `OnReading` cannot run concurrently.

### R-02 — Duplicate `ExtractDeviceId`

**Status:** Proposed  
**Priority:** P1 | **Effort:** S | **Risk:** Low  
**Paths:** `BalanceBoardConnection.cs` (lines ~241–245), `WiimoteCollectionHelper.cs` (lines ~113–120)  
**Issue:** Identical private static regex `e_pid&.*?&(.*?)&` in two files. `DeviceIdRules` today only covers simulated IDs — not HID parsing.  
**Suggested action:** Move to `DeviceIdRules.ExtractFromHidPath(string)` with unit tests (extend `DeviceIdRulesTests.cs`). Use `[GeneratedRegex]` or static compiled regex.

### R-03 — `MainWindow.xaml.cs` is a god object (~630 LOC)

**Status:** Proposed  
**Priority:** P2 | **Effort:** M | **Risk:** Medium  
**Paths:** `MainWindow.xaml.cs`, `MainWindow.xaml`  
**Suggested action:** Extract without full MVVM:
- `ConnectFlowPresenter` — `BeginConnect`, cancel, status strings
- `SettingsUiBinder` — `PopulateUi`, `SyncUiFromSettings`, `SaveSettingsFromUi`
Keep session as single source of truth; no duplicate binding logic.

### R-04 — `BalanceBoardConnection.DiscoverDeviceIds` short-circuit

**Status:** Done (uncommitted)  
**Priority:** P3 | **Effort:** S | **Risk:** Low  
**Paths:** `BalanceBoardConnection.cs`  
**Suggested action:** Good pattern — returns connected device ID without HID probe while connected. Document in WORKFLOW.md; add integration test.

### R-05 — `IBalanceBoardConnection.ReadingAvailable` contract

**Status:** Proposed  
**Priority:** P1 | **Effort:** S | **Risk:** Medium  
**Paths:** `IBalanceBoardConnection.cs`, `BalanceBoard.Testing/FakeBalanceBoardConnection.cs`  
**Suggested action:** Fake connections should raise `ReadingAvailable` if production uses events; today fakes declare the event but never raise it, hiding dual-path bugs in tests.

### R-06 — Preset application in deferred startup

**Status:** Proposed  
**Priority:** P2 | **Effort:** S | **Risk:** Medium  
**Paths:** `MainWindow.RunDeferredStartup`  
**Issue:** Forces game-controller preset when `ActiveProfileName != GameController` on every launch — may surprise users who chose Pedal/Desktop.  
**Suggested action:** Only apply default on first launch (`!HasConnectedBefore`), not every startup.

### R-07 — `SafeCallbacks` scope

**Status:** Proposed  
**Priority:** P3 | **Effort:** S | **Risk:** Low  
**Paths:** `SafeCallbacks.cs`, `BalanceBoardSession.cs`  
**Suggested action:** Session `Log?.Invoke` in catch blocks bypasses `SafeCallbacks` — standardize all external event raises through `SafeCallbacks.Raise`.

### R-08 — Reference parity: `joyResetTimer` (4 min vJoy reset)

**Status:** Proposed  
**Priority:** P3 | **Effort:** M | **Risk:** Low  
**Paths:** `reference/WiiBalanceWalker/FormMain.cs`  
**Suggested action:** Reference periodically reset vJoy; current app does not. Evaluate if still needed with modern vJoy driver; document decision.

---

## Performance

### P-01 — vJoy `WriteAxes` every poll

**Status:** Proposed (likely in flight)  
**Priority:** P1 | **Effort:** S | **Risk:** Low  
**Paths:** `VJoyController.cs`  
**Issue:** `Update` → `WriteAxes` calls `SetAxis` for all 6 axes + `SetBtn` on every invocation with no cached last-values. `OnReading` calls `_vjoy.Update(processed)` every successful poll; dual poll path can push effective rate above 20 Hz. Re-acquire spam was fixed (early-return if same device acquired).  
**Suggested action:** Cache last axis values; call `SetAxis` only on change. Respect `SendCenterOfGravityToAxes` / `SendLoadSensorsToAxes` to skip unused axes.

### P-02 — UI update rate from `OnProcessed`

**Status:** Proposed  
**Priority:** P2 | **Effort:** S | **Risk:** Low  
**Paths:** `MainWindow.xaml.cs`  
**Suggested action:** At 20+ Hz (or 2× with dual poll), `BeginInvoke` queues many delegates. Coalesce UI refresh (e.g. max 30 fps) or use `CompositionTarget.Rendering`.

### P-03 — `ConnectionWorker` queue + poll in same loop

**Status:** Proposed  
**Priority:** P3 | **Effort:** M | **Risk:** Medium  
**Paths:** `ConnectionWorker.cs`  
**Suggested action:** Long-running queued actions can starve poll tick. Consider priority for connect/disconnect vs poll.

### P-04 — Bluetooth discovery during connect

**Status:** Proposed  
**Priority:** P2 | **Effort:** M | **Risk:** Medium  
**Paths:** `BluetoothPairingService.cs`  
**Suggested action:** `DiscoverDevices(255, ...)` is slow. Tune inquiry length; align with in-flight pairing speed work.

### P-05 — Regex per `ExtractDeviceId` call

**Status:** Proposed  
**Priority:** P3 | **Effort:** S | **Risk:** Low  
**Suggested action:** Use `[GeneratedRegex]` or static compiled regex when centralizing in `DeviceIdRules`.

---

## Reliability / Wiimote lifecycle

### W-01 — `WiimoteCollectionHelper` teardown delays

**Status:** Proposed (implemented — document)  
**Priority:** P1 | **Effort:** S | **Risk:** High if removed  
**Paths:** `WiimoteCollectionHelper.cs`, `BalanceConstants.HidCallbackDrainMs`  
**Suggested action:** `ReleaseAll` + `DisconnectGraceMs` + `HidCallbackDrainMs` prevent OnReadData-after-dispose. **Do not shorten without hardware testing.** Promote to AGENTS.md.

### W-02 — `AppContext` thread-pool switch

**Status:** Proposed (implemented — document)  
**Priority:** P1 | **Effort:** S | **Risk:** High  
**Paths:** `App.xaml.cs`  
**Suggested action:** Document why `ThrowOnUncaughtThreadPoolExceptions` is false (WiimoteLib background callbacks).

### W-03 — Connect/disconnect race with in-flight `ConnectWithIntent`

**Status:** Proposed  
**Priority:** P1 | **Effort:** M | **Risk:** High  
**Paths:** `BalanceBoardSession.cs`, `MainWindow.BeginConnect`  
**Suggested action:** `Disconnect()` during active `ConnectWithIntentAsync` needs explicit cancellation + worker drain test.

### W-04 — No reconnect on silent BT drop

**Status:** Proposed  
**Priority:** P1 | **Effort:** L | **Risk:** Medium  
**Paths:** `BalanceBoardSession.Poll`, `BalanceBoardConnection.GetCurrentReading`  
**Issue:** `QuickReconnect` exists for startup and explicit connect (`WakePairedDevices` → HID connect) but there is no automatic recovery when the board drops mid-session. Roadmap Phase 3 item.  
**Suggested action:** Detect stale readings / repeated `ObjectDisposedException` → auto `ConnectWithIntent(QuickReconnect)` with exponential backoff. Surface `StatusChanged` so UI chip reflects reconnecting state. Cap retry count; fall back to "Board offline" with manual Connect button.

### W-05 — `ObjectDisposedException` handling

**Status:** Proposed (partial)  
**Priority:** P2 | **Effort:** S | **Risk:** Medium  
**Paths:** `BalanceBoardConnection.cs`  
**Suggested action:** Surface disconnect to session (`StatusChanged`) so UI updates chip; don't only null `_device` silently.

### W-06 — Feeder cleanup kills `WiiBalanceWalker`

**Status:** Proposed (by design)  
**Priority:** P3 | **Effort:** S | **Risk:** Low  
**Paths:** `FeederProcessCleanup.cs`  
**Suggested action:** Document in README that legacy reference app cannot run alongside; dev `--dev` flag behavior.

---

## Testing

### T-01 — Session dual-path regression test

**Status:** Proposed  
**Priority:** P0 | **Effort:** S | **Risk:** High if missing  
**Suggested action:** Fake connection raises `ReadingAvailable` N times while poll enabled; assert `Processed` / vJoy call count ≤ expected.

### T-02 — `VJoyController` unit tests with mock

**Status:** Proposed  
**Priority:** P2 | **Effort:** M | **Risk:** Medium  
**Suggested action:** Extract `IVJoyNative` wrapper; test acquire retry, busy cleanup path, axis coalescing.

### T-03 — `SettingsStore` migration tests

**Status:** Proposed  
**Priority:** P2 | **Effort:** S | **Risk:** Medium  
**Suggested action:** Golden JSON fixtures for old settings versions → `ApplyMigrations`.

### T-04 — `BalanceBoardConnection` integration with simulated Wiimote

**Status:** Proposed  
**Priority:** P2 | **Effort:** L | **Risk:** Medium  
**Suggested action:** `SimulatedBalanceBoardConnection` exists — add tests for connect/tare/read/dispose order.

### T-05 — Automation: assert no `FATAL` after simulate connect

**Status:** Proposed  
**Priority:** P2 | **Effort:** S | **Risk:** Low  
**Paths:** `tests/BalanceBoard.Automation/SimulateBoardProcessTests.cs`  
**Suggested action:** Extend to grep log for dual-poll indicators or excessive vJoy lines.

### T-06 — Hardware scripts not in CI

**Status:** Proposed  
**Priority:** P3 | **Effort:** S | **Risk:** Low  
**Suggested action:** Expected; document manual cadence in `docs/testing/README.md`.

### T-07 — Update ROADMAP test checkboxes

**Status:** Proposed  
**Priority:** P2 | **Effort:** S | **Risk:** Low  
**Suggested action:** `BalanceProcessor` tests exist (`BalanceProcessorTests.cs`, 15+ facts). Mark done; add remaining gaps instead.

---

## UI/UX architecture

### U-01 — Monolithic dashboard vs wizard

**Status:** Done  
**Note:** `SetupWizardWindow` removed (2026-07-07 UI redesign). `.cursor/rules/wpf-ui.mdc` still references it — **fix rule file**.

### U-02 — Tare/center button enablement

**Status:** Proposed (likely in flight)  
**Priority:** P1 | **Effort:** S | **Risk:** Low  
**Paths:** `MainWindow.xaml`, `BalanceBoardSession.CanSetCenter`  
**Suggested action:** Bind button `IsEnabled` to `CanSetCenter()` / `CanResetCenter()` on each `Processed` event.

### U-03 — Weight threshold hardcoded in UI

**Status:** Proposed  
**Priority:** P2 | **Effort:** S | **Risk:** Low  
**Paths:** `MainWindow.DescribeDirection`, `DescribeActiveInputs`  
**Suggested action:** Use `BalanceConstants.WeightOnBoardThresholdKg`.

### U-04 — Accessibility

**Status:** Proposed  
**Priority:** P3 | **Effort:** M | **Risk:** Low  
**Suggested action:** Roadmap Phase 2 — `AutomationProperties.Name` on connect button, live region for `DirectionText`.

### U-05 — Debug log in production UI

**Status:** Proposed  
**Priority:** P3 | **Effort:** S | **Risk:** Low  
**Suggested action:** Consider collapsible "Advanced" section; keep copy/open folder actions for support.

### U-06 — `ForceShutdown` vs window close

**Status:** Proposed (implemented)  
**Priority:** P2 | **Effort:** S | **Risk:** Medium  
**Suggested action:** Good pattern. `Dispose` is idempotent — document shutdown order in WORKFLOW.md.

---

## Documentation / LLM onboarding

### D-01 — `docs/ARCHITECTURE.md` poll model stale

**Status:** Proposed  
**Priority:** P0 | **Effort:** S | **Risk:** Medium (misleads agents)  
**Issue:** Says `System.Timers.Timer` at 50 ms, "polling only". Code uses `ConnectionWorker` + `ReadingAvailable`.  
**Suggested action:** Update diagram and poll loop section after R-01 decision.

### D-02 — `docs/CODEMAP.md` missing newer files

**Status:** Proposed  
**Priority:** P2 | **Effort:** S | **Risk:** Low  
**Suggested action:** Add `ConnectionWorker`, `WiimoteCollectionHelper`, `SafeCallbacks`, `DeviceIdRules`, `ConnectResult`, `SimulatedBalanceBoardConnection`, test projects (Integration, Fuzz, Automation, Testing).

### D-03 — `AGENTS.md` CI path

**Status:** Proposed  
**Priority:** P3 | **Effort:** S | **Risk:** Low  
**Suggested action:** Says `.github/workflows/build.yml`; actual file is `ci.yml`. Also fix `docs/CODEMAP.md` and `docs/DEVELOPMENT.md` references.

### D-04 — `llms.txt` index

**Status:** Proposed  
**Priority:** P2 | **Effort:** S | **Risk:** Low  
**Suggested action:** Add `docs/suggestions/` backlog, `CONTRIBUTING.md`, `scripts/ci/lint.ps1` as canonical gate.

### D-05 — Python port docs vs event model

**Status:** Proposed  
**Priority:** P2 | **Effort:** S | **Risk:** Medium  
**Paths:** `docs/PYTHON_PORTING.md`, `docs/REFACTORING_FOR_PYTHON.md`  
**Suggested action:** After R-01, state whether Python should use poll timer or HID callbacks.

---

## CI / release hygiene

### CI-01 — Static analysis job is a no-op

**Status:** Proposed  
**Priority:** P3 | **Effort:** S | **Risk:** Low  
**Paths:** `.github/workflows/ci.yml`  
**Suggested action:** Remove or replace with Roslyn report / CodeQL / test coverage upload.

### CI-02 — Release workflow exists

**Status:** Done  
**Paths:** `.github/workflows/release.yml`, `scripts/ci/publish-release.ps1`  
**Suggested action:** Roadmap "GitHub Releases" partially done — mark in ROADMAP.

### CI-03 — `check-crash-safety.ps1` patterns

**Status:** Proposed (good)  
**Priority:** P2 | **Effort:** S | **Risk:** Low  
**Suggested action:** Promote forbidden patterns to CONTRIBUTING.

### CI-04 — Version single source

**Status:** Proposed  
**Priority:** P3 | **Effort:** S | **Risk:** Low  
**Suggested action:** `Directory.Build.props` `<Version>` drives release — document bump process in CONTRIBUTING.

### CI-05 — Dependabot configured

**Status:** Done  
**Paths:** `.github/dependabot.yml`  
**Suggested action:** Verify NuGet + GitHub Actions updates don't auto-merge without lint pass.

---

## Python port readiness

### PY-01 — Pure core is well-factored

**Status:** Promoted (mostly)  
**Paths:** `Processing/BalanceMath.cs`, `ActionEngine.cs`, `MovementMapper.cs`  
**Suggested action:** Continue port order in `REFACTORING_FOR_PYTHON.md`. Golden tests are the contract.

### PY-02 — Session orchestration is Windows-heavy

**Status:** Proposed  
**Priority:** P2 | **Effort:** L | **Risk:** N/A  
**Suggested action:** `ConnectionWorker` + WiimoteLib have no Python equivalent — plan adapter layer first.

### PY-03 — Settings JSON compatibility

**Status:** Proposed  
**Priority:** P2 | **Effort:** S | **Risk:** Medium  
**Suggested action:** Add schema version field to `AppSettings` before Python ships; migrations in `SettingsStore.ApplyMigrations`.

### PY-04 — `ProcessedBalance.Joy*` naming

**Status:** Proposed  
**Priority:** P3 | **Effort:** M | **Risk:** Breaking  
**Suggested action:** `REFACTORING_FOR_PYTHON.md` notes optional `Sensor*` rename — defer until port starts.

---

## Conflicts with existing project rules

| Rule source | Conflict | Resolution |
|-------------|----------|------------|
| INSTRUCTIONS / project-core.mdc | "Commit every change" | User Cursor rule: commit only when asked — reconcile in INSTRUCTIONS |
| ARCHITECTURE.md | "polling only" | Code has event + poll — fix docs after R-01 |
| wpf-ui.mdc | `SetupWizardWindow` | Removed — update rule |
| ROADMAP Phase 3 | "Unit tests for BalanceProcessor" unchecked | Tests exist — update roadmap |
| Minimal abstractions rule | MainWindow god object | Prefer small extract classes over full MVVM |

---

## Comparison with WiiBalanceWalker reference

| Area | Reference (v0.5) | Current app | Notes |
|------|------------------|-------------|-------|
| Poll loop | `System.Timers.Timer` 50 ms on UI thread | `ConnectionWorker` STA + optional events | Safer threading; dual path needs resolution |
| vJoy reset | 4-minute `joyResetTimer` | None | Evaluate necessity |
| Pairing | `FormBluetooth` manual | `BluetoothPairingService` automatic PIN | Improved |
| Input | Monolithic `ActionManager` | `ActionEngine` + `Win32InputBackend` | Better port boundary |
| UI | WinForms `FormMain` | WPF `MainWindow` dashboard | Wizard removed |
| Single instance | None | `SingleInstanceService` | New safety |
| Feeder cleanup | None | `FeederProcessCleanup` | New safety |
| BT reconnect | Manual reconnect | `QuickReconnect` on startup only | W-04: mid-session auto-reconnect missing |
