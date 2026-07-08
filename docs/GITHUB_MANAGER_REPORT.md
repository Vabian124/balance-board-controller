# GitHub Manager Report

**Updated:** 2026-07-09 00:51 UTC+2 (Phase 2 complete — PR #15 open, CI failed at format gate)

## Phase 2 summary

| Item | Value |
|------|-------|
| Branch | `refactor/phase2-full` |
| Base | `main` (`9e3f9d4`) |
| HEAD | `cb4782f` |
| PR commit | Single squashed commit on PR |
| PR | [#15](https://github.com/Vabian124/balance-board-controller/pull/15) — **OPEN** |
| Merge | **NOT merged** (awaiting user) |
| Release / tag | **NONE** (latest remains **v1.5.0**) |

## Triple-check verification

| # | Command | Result |
|---|---------|--------|
| 1 | `gh pr view 15 --json state,statusCheckRollup,url` | **OPEN** — Quality gate **FAILURE** |
| 2 | `gh run list --branch refactor/phase2-full --limit 3` | Latest run `28981156681` → **failure** |
| 3 | `gh run watch 28981156681` | Completed with **failure** (1m18s) |
| 4 | `gh release list` | No new release — still **v1.5.0** |
| 5 | Local `dotnet test` (reported) | **293 passed**, 0 failed |

## Pull request

| Field | Value |
|-------|-------|
| **URL** | https://github.com/Vabian124/balance-board-controller/pull/15 |
| Title | refactor(phase2): Core output pipeline, SettingsSync, and test split |
| State | **OPEN** (not merged) |
| Head SHA | `cb4782f57588f1fdcaacc4316d722b338beb0b94` |

### Phase 2 changes (`cb4782f`)

**Core restructuring**
- Reorganize `BalanceBoard.Core/Services/` into `Connection/`, `Diagnostics/`, `Output/`, `Session/`, `Settings/`
- Move `BalanceProcessor` under `Processing/`; add `FrameOutput`, `OutputRoutingPolicy`, `BalanceReadoutText`, `ConnectionStatusText`
- Extract `ProfileCoordinator` from `BalanceBoardSession`

**UI shell decomposition**
- Split tab views: `DashboardView`, `ProfilesView`, `FineTuningView`, `AdvancedView`
- Shared controls: `ConnectionToolbar`, `SessionLogPanel`
- Extract `SettingsSync` (debounced saves) and `MainWindow.ViewRefs`
- Slim `MainWindow.xaml` / `MainWindow.xaml.cs`

**Tests**
- Split monolithic `BalanceProcessorTests.cs` into focused suites under `Models/` and `Processing/`
- Add `OutputMode`, `FrameOutput`, `ConnectionStatusText` coverage

Zero behavior change claimed; 293 tests pass locally.

## CI status

| Run | Commit | Result | Duration | URL |
|-----|--------|--------|----------|-----|
| **28981156681** | `cb4782f` | **FAIL** | 1m18s | https://github.com/Vabian124/balance-board-controller/actions/runs/28981156681 |
| 28981075025 | (prior push) | failure | 1m21s | https://github.com/Vabian124/balance-board-controller/actions/runs/28981075025 |
| 28980713873 | (prior push) | failure | 1m26s | https://github.com/Vabian124/balance-board-controller/actions/runs/28980713873 |

### Failed step (run 28981156681)

| Job | Step | Result |
|-----|------|--------|
| Quality gate | **Quality gate (format, analyzers, tests, smoke)** | **FAIL** |
| ↳ sub-step | `dotnet format` | exit code 1 |

Pipeline never reached the test stage. Crash-safety grep passed; format gate blocked.

### Root cause

**IDE0130** — Namespace `BalanceBoard.Core.Services` (and `BalanceBoard.Core.Tests`) does not match new folder structure after Phase 2 file moves:

- Core: `Services/Connection/`, `Diagnostics/`, `Output/`, `Session/`, `Settings/` (~24 files)
- Tests: `Models/`, `Processing/` (~10 files)

Additional analyzer warnings also flagged:

- **IDE0011** — Missing braces on `if` in `MainWindow.xaml.cs`
- **IDE0290** — Primary constructor suggestions (`ProfileCoordinator`, `SettingsSync`)
- **JSON002** — Probable JSON strings in `SettingsMigrationsOutputModeTests.cs`

### Fix required (orchestrator / workers)

Run `dotnet format` on branch and commit fixes, **or** add `.editorconfig` / suppressions for intentional flat `BalanceBoard.Core.Services` namespace during folder migration.

## Blockers

| Blocker | Status |
|---------|--------|
| CI format gate red | **BLOCKING merge** |
| PR ready for human review | Yes — code + local tests green; CI format only |
| Release / tag / version bump | None requested or created |

## URLs

- **PR (ready for review, CI red):** https://github.com/Vabian124/balance-board-controller/pull/15
- **CI (latest, failed):** https://github.com/Vabian124/balance-board-controller/actions/runs/28981156681
- **Failed job:** https://github.com/Vabian124/balance-board-controller/actions/runs/28981156681/job/86000000282
- **Compare:** https://github.com/Vabian124/balance-board-controller/compare/main...refactor/phase2-full
