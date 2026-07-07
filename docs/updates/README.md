# Agent update log

Short index for AI assistants. User-facing history: [CHANGELOG.md](../../CHANGELOG.md). Git is the source of truth.

## Recent (newest first)

| Commit | Summary |
|--------|---------|
| [`cf6a36a`](https://github.com/Vabian124/balance-board-controller/commit/cf6a36a) | **Finish-line** — crash-proof disconnect/Minecraft, jump presets, UI detail levels, structured logs |
| [`d7f5f7d`](https://github.com/Vabian124/balance-board-controller/commit/d7f5f7d) | **v1.1.0** — sensitivity presets, dark mode, action mapping UI, balance visual, mouse mode, vJoy idle fix |
| [`5bfb026`](https://github.com/Vabian124/balance-board-controller/commit/5bfb026) | **Connect fix** — removed `NotImplementedException` stubs `dotnet format` injected into `BeginConnect` |
| [`00e88d6`](https://github.com/Vabian124/balance-board-controller/commit/00e88d6) | Portfolio layout, CI quality gate, NetAnalyzers, `scripts/ci/` + `scripts/dev/` |
| [`aebfc01`](https://github.com/Vabian124/balance-board-controller/commit/aebfc01) | **ConnectionWorker** STA thread, test pyramid, crash hardening |
| [`e19b9dd`](https://github.com/Vabian124/balance-board-controller/commit/e19b9dd) | Repo cleanup — `reference/`, canonical `libs/x64/` |

## Older entries

Granular per-session notes (Jul 2026) live in [archive/](archive/). Read only if you need historical context.

## After you commit

Add one line to the table above (commit hash + one sentence). Do not recreate dozens of separate files unless the change is large.

## Quick check

```powershell
git rev-parse --short HEAD
git log -5 --oneline
```
