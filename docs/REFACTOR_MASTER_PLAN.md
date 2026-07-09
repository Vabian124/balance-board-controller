# Refactor Master Plan — Balance Board Controller

**Orchestrator:** `edd330ed`  
**Repo:** `https://github.com/Vabian124/balance-board-controller`  
**Baseline:** `main` @ `ffc3606`; **v1.5.2 shipped** (current stable) @ tag `v1.5.2` — working connect release  
**Last updated:** 2026-07-09

This document synthesizes sibling worker specs into one prioritized execution plan. Full specs live in agent transcripts only — do not duplicate them here.

---

## Status board

| Worker | ID | Status | Key findings |
|--------|-----|--------|--------------|
| Code analysis | `ff75c9d5` | **Done** | `MainWindow` + `BalanceBoardSession` hotspots; `OutputMode` dual-state; test gaps on migration |
| Cleanup audit | `0ccb7db0` | **Done** | Pass 1 safe removals identified (~hundreds of LOC, zero behavior change) |
| Restructure plan | `0607fffb` | **Done** | `FrameOutput` extraction P0; incremental session slimming |
| UX spec | `b9b2162e` | **Done** | Beginner UX gaps; defer UX P0 until Core P0 lands |
| UI spec | `08c38bd9` | **Done** | **SettingsSync** P0; no full MVVM; tab split P1 |
| Backend spec | `92ce83ea` | **Done** | `OutputRoutingPolicy` ≡ `FrameOutput`; OutputMode tests + log key |
| Automation CI | `f5bd74d1` | **Done** | Doc drift, release gates, NuGet cache, job summary |
| Implementation | `3ceb614d` | **Phase 2 done** | PR `refactor/phase2-full` — FrameOutput, ProfileCoordinator, SettingsSync, test split |
| GitHub Manager | `6986e727` | **Finishing** | CI P0 merged (`de834b7`); **pending:** push docs, re-run `v1.5.0` release, wire `verify-version.ps1` |
| Orchestrator | `edd330ed` | **Phase 2 PR** | Branch `refactor/phase2-full`; 293 tests green; no release/tag |

**Progress:** Phase 2 (Core P0 + UI P0 partial) **complete** on `refactor/phase2-full`. Phase 3 UX P0 blocked until merge.

---

## Shipped: v1.5.0

| Item | Detail |
|------|--------|
| Merge | PR #14 → `main` @ `89d9ba7` |
| Tag | `v1.5.0` (`8aafbc0`) |
| Features | `OutputMode`, Minecraft keyboard default, Controlify profile, board-button slot, jump vJoy button, movement hysteresis |
| Release note | GitHub Release workflow failed on first tag push (CI not green yet); **re-run** via `quick-release.ps1 -Tag v1.5.0 -DispatchOnly` after `de834b7` CI passes |

---

## Cross-cutting conclusions

1. **v1.5.0 delivered the OutputMode feature** — enum, presets, migrations, and UI combo; runtime poll path and tests still need consolidation (Core P0).
2. **Architecture is sound** — preserve `ConnectionWorker` STA model, `_pollGate`, vJoy lifecycle, and `ActionPresets` single-source pattern.
3. **Pass 1 cleanup is highest-ROI, zero-risk** — `DebugSessionTrace`, dead exception stack, legacy connect APIs (cleanup audit `0ccb7db0`).
4. **`FrameOutput` unifies restructure + backend specs** — extract output gating from `OnReading` before further session splits.
5. **`SettingsSync` is the UI mirror of Pass 1** — collapse `PopulateUi` / `SyncUiFromSettings` duplication after Core tests exist.
6. **UX P0 waits on Core P0** — error banners and output-mode toasts need stable routing semantics.
7. **CI hygiene largely done** (`de834b7`) — GitHub Manager finishes release re-run + optional `verify-version.ps1` in `release.yml`.
8. **Next tagged release: v1.5.1** — Pass 1 + Core P0 + CI/docs; **v1.6.0** if UX P0 ships in same cycle.

---

## Execution phases (ordered)

```text
Phase 0  [SHIPPED]     v1.5.0 OutputMode (PR #14)
Phase 1  [SHIPPED]     Pass 1 safe cleanup (on main @ 5cfde5b)
Phase 2  [PR OPEN]     Core P0 + UI P0 (SettingsSync, tab views) → refactor/phase2-full
Phase 3  [NEXT]       UX P0 (banners, toasts)    → after Phase 2 merge
Phase 4               Release v1.5.1             → GitHub Manager (no tag in Phase 2 PR)
```

Gate every phase: `scripts/ci/lint.ps1` green before PR merge.

---

## Phase 1 — Pass 1 safe cleanup (implementation)

**Owner:** `3ceb614d`  
**Branch:** `refactor/pass1-cleanup` off `main`  
**Risk:** None (high-confidence dead code per cleanup audit)

| ID | Remove | Files |
|----|--------|-------|
| P1-A | `DebugSessionTrace` + all `Write(...)` call sites (~13) | `DebugSessionTrace.cs`, `BalanceBoardSession.cs`, `BalanceBoardConnection.cs`, `BluetoothPairingService.cs`, `WiimoteCollectionHelper.cs`, `BalanceBoardProtocol.cs` |
| P1-B | `SessionExceptionLog`, `ConnectFlowContext`, `ExceptionLogFormatter` + tests | `Services/*.cs`, `ExceptionLogFormatterTests.cs` |
| P1-C | Legacy connect APIs: `Connect(int)`, `ConnectOrPair(...)`, sync `ConnectWithIntent(...)` | `BalanceBoardSession.cs` |
| P1-D | Dead properties/APIs: `HasPersistedSettings`, `IsBoardVisible()`, `DetectFromSettings()`, `TestEnableVJoyCheck` | `SettingsStore.cs`, `BalanceBoardSession.cs`, `SensitivityPresets.cs`, `MainWindow.xaml.cs` |

**Optional same PR (medium confidence):** collapsed `EnableVJoyCheck` / `DisableActionsCheck` in XAML + save paths (after `OutputModeCombo` parity confirmed).

**Do not touch in Pass 1:** `SettingsStore.ApplyMigrations`, `EnableVJoy`/`DisableKeyboardActions` JSON fields, `ConnectionFlowLogger`.

---

## Phase 2 — Core P0 (implementation)

**Owner:** `3ceb614d`  
**Branch:** `refactor/p0-output-pipeline` (after Pass 1 merges)

| # | Item | Files |
|---|------|-------|
| P0-C1 | Extract **`FrameOutput`** from `OnReading` (identical truth table to `OutputRoutingPolicy`) | `Processing/FrameOutput.cs`, `BalanceBoardSession.cs` |
| P0-C2 | **`FrameOutputTests`** — `(OutputMode, BoardButton kind)` matrix | `tests/.../FrameOutputTests.cs` |
| P0-C3 | **`OutputMode` / `SetOutputMode` tests** + migration JSON fixtures | `AppSettingsTests.cs`, `SettingsStoreProfileTests.cs` |
| P0-C4 | **BoardButton-key-in-vJoy-mode** integration test | `InputSimulationFlowTests.cs` |
| P0-C5 | Extend **`LoadSettings` log key** with `OutputMode` | `BalanceBoardSession.cs` |
| P0-C6 | **Split `BalanceProcessorTests.cs`** (move only) | `tests/BalanceBoard.Core.Tests/Processing/*`, `Models/*` |

---

## Phase 3 — UI P0 (implementation)

**Owner:** `3ceb614d`  
**Depends on:** Phase 2 merge recommended

| # | Item | Files |
|---|------|-------|
| P0-U1 | Extract **`SettingsSync`** — single settings ↔ controls map | `Services/SettingsSync.cs`, `MainWindow.xaml.cs` |
| P0-U2 | Debounce slider saves in `SaveSettingsFromUi` | `MainWindow.xaml.cs` |
| P0-U3 | Move readout helpers to **`BalanceDisplay`** | Core + tests |

---

## Phase 4 — UX P0 (after Core P0)

Defer until Phase 2 on `main`. See UX backlog below.

---

## Phase 5 — Release (GitHub Manager)

**Owner:** `6986e727`

| Step | Action | Status |
|------|--------|--------|
| 1 | Push `main` (`cda54d0`, `de834b7`, plan updates) | **Pending** |
| 2 | Verify CI green on latest `main` | After push |
| 3 | Re-run **v1.5.0** release (`quick-release.ps1 -Tag v1.5.0 -DispatchOnly`) | **Pending** |
| 4 | Wire `verify-version.ps1` into `release.yml` | **Deferred** from `de834b7` |
| 5 | After Pass 1 + Core P0 merge → **v1.5.1** tag + release | Blocked on implementation |

---

## UX backlog (summary)

**Rule:** Start UX P0 only after **Phase 2 (Core P0)** merges.

| ID | Item | Cross-link |
|----|------|------------|
| P0-1 | Dashboard **error banner** | UI SettingsSync + debounce |
| P0-2 | **vJoy chip popover** | Backend `LastError` passthrough (P1) |
| P0-3 | **Expose auto-connect in Beginner** | UI ThemeCard visibility bug (`ApplyDetailLevel`) |
| P0-4 | Unify profile picker (Controlify card) | `ActionPresets` |
| P0-5 | **Output-mode change toast** | Core OutputMode consolidation |
| P0-6 | Post-connect calibration coach | Dashboard only |

---

## Automation (completed + remaining)

| Item | Status |
|------|--------|
| Doc sync (CONTRIBUTING, AGENTS, PR template) | **Done** (`de834b7`) |
| NuGet cache in `ci.yml` | **Done** |
| `fail_on_unmatched_files: true` | **Done** |
| CI job summary from `summary.json` | **Done** |
| `scripts/ci/verify-version.ps1` | **Created**; wire in `release.yml` → GitHub Manager |
| Pre-commit hook, PR fast job | P1 |

---

## Conflict resolution

| Topic | Decision |
|-------|----------|
| `FrameOutput` vs `OutputRoutingPolicy` | **Single `FrameOutput`** in `Processing/` |
| Remove legacy output flags | **Keep JSON fields** through v1.5.1; gate via `FrameOutput`; UI checkboxes in Pass 1 optional |
| Pass 1 before Core P0 | **Yes** — cleanup audit + code analysis agree; reduces noise before extraction |
| Full MVVM | **No**; optional `SettingsViewModel` in P3 only |
| Release version | **v1.5.1** for refactor pass; **v1.6.0** if UX P0 included |

---

## Implementation worker directives

**Worker:** `3ceb614d`

1. **Now:** `refactor/pass1-cleanup` — implement Phase 1 table (P1-A through P1-D).
2. **Then:** `refactor/p0-output-pipeline` — Phase 2 (P0-C1–C6).
3. **Then:** Phase 3 UI P0 (P0-U1–U3) in same or follow-on PR.
4. Run `scripts/ci/lint.ps1` before each PR; update `docs/CODEMAP.md` if files removed/added.
5. Do **not** tag or bump version — GitHub Manager owns releases.
6. Add `docs/updates/README.md` row per merged PR.

---

## GitHub Manager directives

**Worker:** `6986e727`

1. **Push** all unpushed commits on `main` to `origin` (includes `cda54d0`, `de834b7`, plan updates).
2. Confirm CI green on pushed HEAD.
3. **Re-run v1.5.0 release** (workflow failed on first attempt — see `docs/GITHUB_MANAGER_REPORT.md`).
4. Wire `verify-version.ps1` into `release.yml` when convenient.
5. Track implementation PRs; tag **v1.5.1** only after Pass 1 + Core P0 merge + full lint.
6. Never force-push `main`.

---

## Release readiness gate

```powershell
dotnet build BalanceBoard.sln -c Release
dotnet test BalanceBoard.sln -c Release
dotnet run --project tools/Validate/BalanceBoard.Validate.csproj -c Release
./scripts/ci/lint.ps1
```

| Check | Current |
|-------|---------|
| v1.5.0 feature code on `main` | **Yes** (`89d9ba7`) |
| v1.5.0 GitHub Release artifact | **Retry needed** |
| Pass 1 cleanup PR | **Not opened** |
| Core P0 PR | **Blocked** on Pass 1 (recommended) |
| CI P0 on `main` | **Done** (`de834b7`) |

---

## Worker transcript references

| Worker | Transcript ID |
|--------|---------------|
| Code analysis | `ff75c9d5-f235-430f-b63b-a6da81d04605` |
| Cleanup audit | `0ccb7db0-f59e-4cb2-b5a5-b0fa8d7b3aad` |
| Restructure | `0607fffb-d625-484c-a6dd-f0dc8c6db37a` |
| UX | `b9b2162e-5b7a-4520-9ef1-0b5523258739` |
| UI | `08c38bd9-0d96-45c9-94f1-30ddff6b0ed7` |
| Backend | `92ce83ea-6a1e-455f-bb31-268973fa0b62` |
| Automation | `f5bd74d1-b3f5-4255-bba5-53a344142977` |
| Implementation | `3ceb614d-5d5a-4258-8684-d966ec84beec` |
| GitHub Manager | `6986e727-7682-4ea8-bdd8-073e379cb49b` |
| Orchestrator | `edd330ed-33f8-435f-bb51-610bb253f625` |
