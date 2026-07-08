# GitHub Manager Report

**Updated:** 2026-07-08 (verification pass — triple-checked with live `gh` commands)

## Verification checklist

| # | Check | Result | Details |
|---|-------|--------|---------|
| 1 | `git fetch && git status` | **PASS** | `main` synced with `origin/main` at `5cfde5b` (pre-verify-version commit) |
| 2 | `gh pr list --state all --limit 5` | **PASS** | Latest merged: [#14](https://github.com/Vabian124/balance-board-controller/pull/14) (v1.5.0 refactor) |
| 3 | `gh release list --limit 5` | **PASS** | Latest: **v1.5.0** (2026-07-08T21:45:56Z) |
| 4 | `gh release view v1.5.0` | **PASS** | Published, not draft/prerelease; both assets present |
| 5 | Release zip asset | **PASS** | `BalanceBoardController-1.5.0-win-x64.zip` — 561,475 bytes, state `uploaded` |
| 6 | Release sha256 asset | **PASS** | `BalanceBoardController-1.5.0-win-x64.zip.sha256` — 106 bytes, state `uploaded` |
| 7 | CI on `5cfde5b` (main HEAD) | **PASS** | Run [28978343001](https://github.com/Vabian124/balance-board-controller/actions/runs/28978343001) — success, 3m18s |
| 8 | Local vs remote sync | **PASS** | No unpushed commits before verify-version wiring commit |
| 9 | `verify-version` in `release.yml` | **PENDING** | Local change committed this pass; see below |
| 10 | Re-dispatch v1.5.0 release | **N/A** | Release already published via dispatch run [28977875731](https://github.com/Vabian124/balance-board-controller/actions/runs/28977875731) |

## Current repo state

| Item | Value |
|------|-------|
| Branch | `main` |
| HEAD (after this pass) | verify-version wiring commit on top of `5cfde5b` |
| Remote | `origin` → `https://github.com/Vabian124/balance-board-controller.git` |
| Version | **1.5.0** (`Directory.Build.props`) |
| Latest git tag | `v1.5.0` → `89d9ba7` |
| Latest GitHub Release | **v1.5.0** |

## Release

| Field | Value |
|-------|-------|
| URL | https://github.com/Vabian124/balance-board-controller/releases/tag/v1.5.0 |
| Published | 2026-07-08T21:45:56Z |
| Draft | false |
| Prerelease | false |
| Zip download | https://github.com/Vabian124/balance-board-controller/releases/download/v1.5.0/BalanceBoardController-1.5.0-win-x64.zip |
| SHA256 download | https://github.com/Vabian124/balance-board-controller/releases/download/v1.5.0/BalanceBoardController-1.5.0-win-x64.zip.sha256 |

**Note:** Initial tag-push release run [28977824679](https://github.com/Vabian124/balance-board-controller/actions/runs/28977824679) failed (CI race). Successful re-dispatch via [28977875731](https://github.com/Vabian124/balance-board-controller/actions/runs/28977875731).

## CI on main (recent)

| Run | Commit | Result | URL |
|-----|--------|--------|-----|
| 28978343001 | `5cfde5b` cleanup | **success** | https://github.com/Vabian124/balance-board-controller/actions/runs/28978343001 |
| 28978142307 | `d91b937` docs | cancelled (concurrency) | https://github.com/Vabian124/balance-board-controller/actions/runs/28978142307 |
| 28978012031 | `de834b7` P0 CI | cancelled (concurrency) | https://github.com/Vabian124/balance-board-controller/actions/runs/28978012031 |
| 28977800468 | `89d9ba7` PR #14 merge | **success** | https://github.com/Vabian124/balance-board-controller/actions/runs/28977800468 |

Cancelled runs are expected: `ci.yml` concurrency group cancels superseded runs when newer pushes land on `main`.

## P0 automation (complete)

| Item | Status |
|------|--------|
| Docs sync (CONTRIBUTING, AGENTS, PR template) | Done (`de834b7`) |
| NuGet cache in `ci.yml` | Done (`de834b7`) |
| `fail_on_unmatched_files: true` in release | Done (`de834b7`) |
| CI job summary from `summary.json` | Done (`de834b7`) |
| `scripts/ci/verify-version.ps1` | Done (`de834b7`) |
| Wire `verify-version` into `release.yml` | Done (this pass) |

## Blockers

None. v1.5.0 release is live with both assets. No manual GitHub action required.

## User verification URLs (optional)

- Release page: https://github.com/Vabian124/balance-board-controller/releases/tag/v1.5.0
- Latest CI: https://github.com/Vabian124/balance-board-controller/actions/workflows/ci.yml
- Release workflow: https://github.com/Vabian124/balance-board-controller/actions/workflows/release.yml
