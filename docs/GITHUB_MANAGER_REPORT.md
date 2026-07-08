# GitHub Manager Report

**Updated:** 2026-07-08 (orchestrator P0 CI pass)

## Current repo state

| Item | Value |
|------|-------|
| Branch | `main` (clean working tree after this commit) |
| HEAD | `89d9ba7` — Merge PR #14 (output-mode refactor) |
| Remote | `origin` → `https://github.com/Vabian124/balance-board-controller.git` |
| Version (`Directory.Build.props`) | **1.5.0** |
| Latest git tag | `v1.5.0` (local + remote) |
| Latest GitHub Release | **v1.4.2** (v1.5.0 release workflow failed) |

## Pull requests

| PR | Title | Status | URL |
|----|-------|--------|-----|
| #14 | v1.5.0: OutputMode refactor | **MERGED** | https://github.com/Vabian124/balance-board-controller/pull/14 |
| #13 | User-reported bug fixes | MERGED | https://github.com/Vabian124/balance-board-controller/pull/13 |

No open PRs at time of report.

## CI / release status

| Workflow | Last run | Result | Notes |
|----------|----------|--------|-------|
| CI (`ci.yml`) on `main` | Post-merge #14 | **SUCCESS** | Quality gate green on `89d9ba7` |
| Release (`release.yml`) on `v1.5.0` tag | Run 28977824679 | **FAILED** | Tag pushed before post-merge CI finished; "No successful CI run on 89d9ba7" |

### Release recovery (v1.5.0)

After CI is green on `main` (it is now), re-run release:

```powershell
.\scripts\release\quick-release.ps1 -Tag v1.5.0 -DispatchOnly
```

Or GitHub Actions → **Release** → Run workflow → tag `v1.5.0`.

## P0 automation improvements (this pass)

### Implemented

1. **Docs sync** — `CONTRIBUTING.md`, `AGENTS.md`, `.github/pull_request_template.md` now reference the unified test pipeline (`BalanceBoard.App.Ui.Tests`, `BalanceBoard.Automation`) instead of legacy `UiSmoke` / `test-flow.ps1`.
2. **NuGet cache** — `actions/cache@v4` on `~/.nuget/packages` in `ci.yml`.
3. **`fail_on_unmatched_files: true`** — `release.yml` now fails if `dist/*.zip` artifacts are missing.
4. **CI job summary** — `ci.yml` writes a markdown table from `artifacts/test/summary.json` to `GITHUB_STEP_SUMMARY`.
5. **`scripts/ci/verify-version.ps1`** — validates `Directory.Build.props` version matches `CHANGELOG.md` section; optional `-Tag` check. **Not yet wired into release.yml** (deferred).

### Deferred

| Item | Reason |
|------|--------|
| Wire `verify-version.ps1` into `release.yml` | Script created; orchestrator can add step after `checkout` in a follow-up |
| v1.5.0 GitHub Release publish | Blocked by failed release run; needs manual re-dispatch (see above) |
| Broader doc sweep (`DEVELOPMENT.md`, `CODEMAP.md`, `README.md`) | Still mention `UiSmoke` as legacy; out of P0 scope but noted for spec worker |

## Versioning

| Source | Version |
|--------|---------|
| `Directory.Build.props` | 1.5.0 |
| `CHANGELOG.md` | `[1.5.0]` section present |
| Git tag | `v1.5.0` exists |
| GitHub Release | **Missing** — needs re-run |

## Blockers

1. **v1.5.0 GitHub Release** — tag exists but release workflow failed (CI race). Re-dispatch when ready.
2. **`verify-version.ps1` not in CI/release** — script ready, wiring deferred.

## Build verification

```
dotnet build BalanceBoard.sln -c Release  →  SUCCESS (0 warnings, 0 errors)
scripts/ci/verify-version.ps1 -Tag v1.5.0  →  SUCCESS
```
