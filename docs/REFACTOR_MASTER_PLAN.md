# Refactor Master Plan — Balance Board Controller

**Orchestrator:** `edd330ed`  
**Repo:** `https://github.com/Vabian124/balance-board-controller`  
**Baseline:** `main` @ `89d9ba7` (v1.5.0 merged, PR #14)  
**Last updated:** 2026-07-08

This document synthesizes sibling worker specs into one prioritized execution plan. Full specs live in agent transcripts only — do not duplicate them here.

---

## Status board

| Worker | ID | Status | Key findings |
|--------|-----|--------|--------------|
| Code analysis | `ff75c9d5` | **Done** | `MainWindow` (~2,165 LOC) + `BalanceBoardSession` (~1,389 LOC) are complexity hotspots; `OutputMode` dual-state with legacy flags; strong Processing tests; gaps on `OutputMode`/migration |
| Cleanup audit | `0ccb7db0` | **Pending** | Started scan only — backlog not delivered |
| Restructure plan | `0607fffb` | **Done** | Incremental extraction: `FrameOutput`, session slimming, test file split; defer namespace/folder moves |
| UX spec | `b9b2162e` | **Done** | Beginner UX gaps (hidden auto-connect, vJoy remediation, error banner); defer UX P0 until Core P0 |
| UI spec | `08c38bd9` | **Done** | No full MVVM; **SettingsSync** P0; tab split P1; debounced saves |
| Backend spec | `92ce83ea` | **Done** | `OutputRoutingPolicy` ≡ `FrameOutput`; migration/tests; extend settings log key |
| Automation CI | `f5bd74d1` | **Done** | Doc drift vs CI; release version gates; NuGet cache; job summary |
| Implementation | `3ceb614d` | **Pending** | Assigned Core P0 below; v1.5.0 work already merged |
| GitHub Manager | `6986e727` | **In progress** | CI P0 automation delegated; report pending |

**Progress:** 6 of 9 workers complete; 3 pending (cleanup, implementation, GitHub Manager).

---

## Cross-cutting conclusions

1. **Architecture is sound** — App/Core split, `ConnectionWorker` STA model, `Processing/` portability, and `ActionPresets` single-source pattern should not be rewritten.
2. **Complexity is concentrated** — `BalanceBoardSession.OnReading` output gating and `MainWindow` settings sync triangle are the highest-ROI extraction targets across Core + UI specs.
3. **`OutputMode` refactor shipped in v1.5.0 but is incomplete** — enum + `SetOutputMode()` exist; poll path still gates on `EnableVJoy` / `DisableKeyboardActions`; tests and log keys lag behind.
4. **`FrameOutput` and `OutputRoutingPolicy` are the same target** — one class in `Processing/FrameOutput.cs` with policy methods; restructure + backend workers agree.
5. **Tests before deletes** — add characterization tests for output routing and `OutputMode` migration before removing hidden legacy checkboxes or `DebugSessionTrace` calls.
6. **UX improvements depend on Core clarity** — error banners, output-mode toasts, and vJoy chip popovers need stable `OutputMode` semantics and optional session status passthrough.
7. **CI/release hygiene is independent** — doc sync, NuGet cache, and version gates can land on `main` without touching runtime behavior (GitHub Manager).
8. **Next release is a refactor patch/minor** — suggest **v1.5.1** (tests + extractions, no user-facing feature claims) or **v1.6.0** if UX P0 ships in same cycle.

---

## Cross-cutting P0 (implementation consensus)

Execute in order on feature branch `refactor/p0-output-pipeline` off `main`. **Implementation worker `3ceb614d` owns items 1–6.** Gate: `scripts/ci/lint.ps1` green before PR.

| # | Item | Owner | Files | Depends on |
|---|------|-------|-------|------------|
| P0-C1 | **Extract `FrameOutput`** (aka `OutputRoutingPolicy`) from `OnReading`; identical truth table | Impl | `Processing/FrameOutput.cs`, `BalanceBoardSession.cs` | — |
| P0-C2 | **`FrameOutputTests`** — matrix `(OutputMode, BoardButton kind)` → vJoy vs keyboard sinks | Impl | `tests/.../FrameOutputTests.cs` | P0-C1 |
| P0-C3 | **`OutputMode` / `SetOutputMode` tests** + migration fixtures (JSON without `OutputMode`) | Impl | `AppSettingsTests.cs`, `SettingsStoreProfileTests.cs` | — |
| P0-C4 | **BoardButton-key-in-vJoy-mode** integration test (`DisableKeyboardActions=true` + Escape still sent) | Impl | `InputSimulationFlowTests.cs` | — |
| P0-C5 | **Extend `LoadSettings` log key** with `OutputMode` | Impl | `BalanceBoardSession.cs` | — |
| P0-C6 | **Split `BalanceProcessorTests.cs`** into topic files (move only, no assertion changes) | Impl | `tests/BalanceBoard.Core.Tests/Processing/*`, `Models/*` | — |
| P0-C7 | **Extract `SettingsSync`** — unify `PopulateUi` / `SyncUiFromSettings` / `SaveSettingsFromUi` field map | Impl | `Services/SettingsSync.cs`, `MainWindow.xaml.cs` | P0-C1–C5 recommended first |
| P0-C8 | **CI doc sync + workflow hardening** | GitHub Mgr `6986e727` | `CONTRIBUTING.md`, `AGENTS.md`, `.github/*`, `scripts/ci/verify-version.ps1` | — |

**Explicitly deferred until P0-C1–C5 merge:** UX P0 (banners, toasts, ThemeCard fix), hidden checkbox removal, session connect/recovery extraction (P1).

---

## Phased roadmap

### P0 — Safe cleanups + pipeline clarity (current sprint)

- Core: P0-C1 through P0-C6 (above)
- CI: P0-C8 (GitHub Manager)
- Cleanup audit (`0ccb7db0`): feed high-confidence dead-code list into P0 after audit completes

### P1 — Structure without behavior change

| Area | Items |
|------|-------|
| Core | `ProfileCoordinator`; `SettingsMigrations` extract; `BalanceProcessor` → `Processing/` move |
| UI | Tab UserControls (`DashboardView`, etc.); debounced slider saves; preset handler consolidation |
| UX | P1 backlog (profile badge, Beginner labels, MY PROFILES collapse) |
| CI | Pre-commit hook (format + crash-safety); `scripts/dev/check.ps1`; PR fast job (`test.ps1 -Quick`) |

### P2 — Session slimming + UX polish

| Area | Items |
|------|-------|
| Core | `SessionRecovery`, `ConnectFlow` extraction (sub-steps, fake-backed tests) |
| UI | `ConnectCoordinator`, `VJoySettingsPanel`, `ActionBindingsPanel` |
| UX | P2 toasts, visual polish, a11y icons |
| CI | Coverlet report-only; CodeQL; nightly full CI |

### P3 — Automation & distribution

- Self-contained release zip; post-publish smoke; MSIX/installer (ROADMAP Phase 4)
- Optional MVVM increment (`SettingsViewModel` for Fine Tuning only)

---

## UX backlog (summary)

**Rule:** Defer **UX P0 implementation** until **Core P0-C1–C5** merge to `main`.

### UX P0 (after Core P0)

| ID | Item | Cross-link |
|----|------|------------|
| P0-1 | Dashboard **error banner** (Beginner-safe connect/vJoy failures) | UI **SettingsSync** + debounce reduces save noise before banner work |
| P0-2 | **vJoy chip popover** (Install / Configure / status) | Backend optional `VJoyController.LastError` passthrough (P1) |
| P0-3 | **Expose auto-connect + start minimized in Beginner** | UI spec **ThemeCard visibility bug** — `ApplyDetailLevel` hides `ThemeCard` in Simple |
| P0-4 | Unify profile picker (Controlify card + combo sync) | `ActionPresets` already canonical |
| P0-5 | **Output-mode change toast** when binding forces keyboard | Backend **OutputMode consolidation** + UI `ActionBinding_Changed` |
| P0-6 | Post-connect calibration coach (Tare → step on → Set center) | Dashboard only; no Core change |

### UX P1 (selected)

- Rename Beginner / Feel / Advanced labels; active profile + output badge on Dashboard
- Collapse MY PROFILES in Beginner; first-launch welcome card
- Binding conflict warnings; move Exit off Dashboard strip
- Disconnected placeholder for `BalanceBoardVisual`; chip icons; auto-expand log on errors

Full UX P2 list: see worker `b9b2162e` transcript.

---

## UI backlog (summary)

| Priority | Item | Files |
|----------|------|-------|
| **P0** | `SettingsSync` extraction | `MainWindow.xaml.cs` → `Services/SettingsSync.cs` |
| **P0** | Debounce slider saves | `MainWindow.xaml.cs` |
| **P0** | Move readout helpers to `BalanceDisplay` | Core + tests |
| **P1** | Tab UserControls | `Views/*` |
| **P1** | `ConnectionToolbar`, `SessionLogPanel` | `Controls/*` |
| **Defer** | Full MVVM | — |

---

## Backend backlog (summary)

| Priority | Item | Notes |
|----------|------|-------|
| **P0** | `FrameOutput` / `OutputRoutingPolicy` | Same as P0-C1 |
| **P0** | OutputMode tests + log key | P0-C3, P0-C5 |
| **P1** | `BluetoothRecoveryService`, `ConnectionFlowService`, `SessionHealthMonitor` | Large; after P0 |
| **P2** | UI routes all output changes through `SetOutputMode`; remove legacy checkbox writes | Coordinate with UX P0-5 |

---

## Automation backlog (summary)

| Priority | Item | Owner |
|----------|------|-------|
| **P0** | Sync CONTRIBUTING / AGENTS / PR template with 5-layer CI | GitHub Mgr |
| **P0** | NuGet cache in `ci.yml` | GitHub Mgr |
| **P0** | `fail_on_unmatched_files: true` in `release.yml` | GitHub Mgr |
| **P0** | CI job summary from `artifacts/test/summary.json` | GitHub Mgr |
| **P0** | `scripts/ci/verify-version.ps1` + wire in `release.yml` | GitHub Mgr |
| **P1** | Pre-commit hook; `verify-docs.ps1`; PR fast job | GitHub Mgr / later |

---

## Conflict resolution (pragmatic picks)

| Disagreement | Resolution |
|--------------|------------|
| `FrameOutput` vs `OutputRoutingPolicy` vs `OutputDispatcher` | **Single class `FrameOutput`** in `Processing/` with static policy methods; session calls `FrameOutput.Apply(...)` |
| Full MVVM vs code-behind | **No full MVVM**; optional `SettingsViewModel` in P3 only |
| Big-bang `Services/` subfolders vs status quo | **Physical subfolders in P5** (restructure plan); defer namespaces |
| Remove `EnableVJoy` / `DisableKeyboardActions` now vs later | **Keep JSON fields**; gate through `FrameOutput`; UI hidden checkboxes removed in P2 after tests |
| Direct commits to `main` vs PR | **Feature branch + PR** for P0-C1–C7; **direct to `main`** acceptable for CI-only P0-C8 |
| v1.5.1 vs v1.6.0 | **v1.5.1** if only P0 Core+CI; **v1.6.0** if UX P0 included |

---

## GitHub Manager directives

**Worker:** `6986e727`  
**Reports to:** Orchestrator `edd330ed`  
**Coordination:** Update `docs/GITHUB_MANAGER_REPORT.md` after each GitHub action.

### Immediate (P0 — implement on `main` or `chore/ci-p0-hardening`)

1. Sync stale docs: `CONTRIBUTING.md`, `AGENTS.md`, `.github/pull_request_template.md` — reference `BalanceBoard.App.Ui.Tests` + `BalanceBoard.Automation`, not `tools/UiSmoke` / `test-flow.ps1`.
2. Add NuGet cache to `.github/workflows/ci.yml` (`actions/cache@v4`, key from `**/*.csproj` + `global.json`).
3. Set `fail_on_unmatched_files: true` in `.github/workflows/release.yml`.
4. Add CI step: read `artifacts/test/summary.json` → `$GITHUB_STEP_SUMMARY`.
5. Create `scripts/ci/verify-version.ps1` (tag ↔ `Directory.Build.props` ↔ `CHANGELOG.md`); wire into `release.yml` pre-package step.

### After implementation worker opens PR

1. Create/track PR for `refactor/p0-output-pipeline` → `main`.
2. Require green **Quality gate** (`ci.yml`) before merge.
3. Squash merge preferred for refactor PR.

### Release (after P0 merge + full lint)

1. Propose version: **v1.5.1** (refactor + CI hardening).
2. Run `scripts/release/bump-and-tag.ps1 -Version 1.5.1 -Commit` + `CHANGELOG.md` section.
3. Wait for green CI on release commit → `git push origin v1.5.1` (or `quick-release.ps1`).
4. Verify `release.yml` packages zip + `.sha256`; confirm GitHub Release notes.
5. Push all commits; never force-push `main`.

### Blockers to report upstream

- Open dependabot PRs (non-blocking for refactor).
- Cleanup audit incomplete — do not remove code until `0ccb7db0` delivers list.
- Implementation worker not started — release tag **blocked** until P0-C1–C6 PR merges.

---

## Implementation worker directives

**Worker:** `3ceb614d`

1. Branch from `main`: `refactor/p0-output-pipeline`.
2. Implement P0-C1 → P0-C6 in order; characterization tests before moving `OnReading` branches.
3. P0-C7 (`SettingsSync`) may follow in same PR or second PR after Core tests green.
4. Do **not** remove `DebugSessionTrace` calls until cleanup audit signs off.
5. Do **not** tag or bump version — GitHub Manager owns release.
6. Update `docs/CODEMAP.md` only if files added/renamed.
7. Add `docs/updates/README.md` row on merge.

---

## Release readiness gate

Before any tag:

```powershell
dotnet build BalanceBoard.sln -c Release
dotnet test BalanceBoard.sln -c Release
dotnet run --project tools/Validate/BalanceBoard.Validate.csproj -c Release
./scripts/ci/lint.ps1   # full CI parity locally
```

| Check | Current (`89d9ba7`) |
|-------|---------------------|
| On `main`, clean tree | Yes |
| Latest release | **v1.5.0** (PR #14) |
| Refactor release | **Blocked** — P0 implementation PR not opened |
| CI P0 | **In progress** (GitHub Manager) |

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
