# GitHub Manager Report

**Updated:** 2026-07-09 — **v1.5.2 shipped** (current stable / working connect release)

## Current release

| Item | Value |
|------|-------|
| **Current stable** | **v1.5.2** |
| Tag | [`v1.5.2`](https://github.com/Vabian124/balance-board-controller/releases/tag/v1.5.2) (`ffc3606`) |
| Release | [GitHub Release v1.5.2](https://github.com/Vabian124/balance-board-controller/releases/tag/v1.5.2) |
| Tests | **309 passed** (reference connect fixtures, FormBluetooth inline wake, connect hardening) |
| Branch | `main` @ `ffc3606` |

## Prior Phase 2 summary (historical)

| Item | Value |
|------|-------|
| Branch | `refactor/phase2-full` (merged) |
| PR | [#15](https://github.com/Vabian124/balance-board-controller/pull/15) — merged as v1.5.1 |
| Release / tag before v1.5.2 | v1.5.1, v1.5.0 |

## Triple-check verification (v1.5.2)

| # | Command | Result |
|---|---------|--------|
| 1 | `git tag -l 'v1.5.2'` | Tag present |
| 2 | `Directory.Build.props` `<Version>` | **1.5.2** |
| 3 | `CHANGELOG.md` | `## [1.5.2]` section marked stable |
| 4 | `dotnet test BalanceBoard.sln -c Release` | **309 passed** |
| 5 | `gh release view v1.5.2` | Published on GitHub |

## Pull request (historical — Phase 2)

| Field | Value |
|-------|-------|
| PR | [#15](https://github.com/Vabian124/balance-board-controller/pull/15) |
| Status | **Merged** → v1.5.1 |

## Release workflow

- `release.yml` runs on version tags; `verify-version.ps1` checks `Directory.Build.props` + `CHANGELOG.md`.
- Re-publish: `.\scripts\release\quick-release.ps1 -Tag v1.5.2 -DispatchOnly`

## Not done / deferred

| Item | Status |
|------|--------|
| Wire `verify-version.ps1` mandatory gate in all release paths | Optional hardening |
| MSIX / Inno installer | ROADMAP Phase 4 |
| Auto-update check | ROADMAP Phase 4 |
