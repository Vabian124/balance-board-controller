# Agent update log

> **For AI assistants:** Read this folder to see **what was done, when, and in which commit** — so you do not confuse past work, current repo state, and planned future work.

## How to use this log

1. **Read newest file last** — files are named with date + time + short commit hash (chronological).
2. **Match commit hash** — each entry links to an exact `git` commit on `main`. Run `git log --oneline` and compare.
3. **Past vs present vs future**
   - **Past** = entries with commits **before** `HEAD`; already shipped.
   - **Present** = latest entry matches `git rev-parse --short HEAD`.
   - **Future** = items listed under *Not done / left for later* in any entry, or [ROADMAP.md](../ROADMAP.md) — **not implemented** unless a newer update says otherwise.
4. **Adding a new entry** — when you finish meaningful work and commit:
   - Create `YYYY-MM-DD_HHMMSS_<short-hash>_<slug>.md` (use commit author date from `git log`).
   - Append a row to the index table below.
   - Reference the full hash and list what changed vs what was explicitly deferred.

## Index (chronological)

| # | Date (UTC+2) | Commit | Title | Agent session |
|---|--------------|--------|-------|---------------|
| 1 | 2026-07-07 13:02:26 | [`4a0b399`](https://github.com/Vabian124/balance-board-controller/commit/4a0b399) | [Initial modern app release](2026-07-07_130226_4a0b399_initial-app-release.md) | Cursor — WiiBalanceWalker revamp |
| 2 | 2026-07-07 13:10:44 | [`fb82a5d`](https://github.com/Vabian124/balance-board-controller/commit/fb82a5d) | [Profile presets, input, CI](2026-07-07_131044_fb82a5d_profile-presets-and-ci.md) | Cursor — finish todos, lint, push |
| 3 | 2026-07-07 13:14:39 | [`238405a`](https://github.com/Vabian124/balance-board-controller/commit/238405a) | [LLM-friendly docs and Cursor rules](2026-07-07_131439_238405a_llm-friendly-docs.md) | Cursor — make repo LLM-friendly |
| 4 | 2026-07-07 13:19:23 | [`5dff713`](https://github.com/Vabian124/balance-board-controller/commit/5dff713) | [Agent update log folder](2026-07-07_131923_5dff713_agent-update-log.md) | Cursor — update MD audit trail |
| 5 | 2026-07-07 13:23:44 | [`b451e12`](https://github.com/Vabian124/balance-board-controller/commit/b451e12) | [Every-pass LLM instructions](2026-07-07_132344_b451e12_llm-instructions.md) | Cursor — INSTRUCTIONS.md playbook |
| 6 | 2026-07-07 13:29:02 | [`ae01e12`](https://github.com/Vabian124/balance-board-controller/commit/ae01e12) | [Startup fixes and first-run wizard](2026-07-07_132902_ae01e12_startup-wizard-fix.md) | Cursor — first launch for user |
| 7 | 2026-07-07 13:44:30 | [`7c1859c`](https://github.com/Vabian124/balance-board-controller/commit/7c1859c) | [Automatic Bluetooth pairing](2026-07-07_134430_7c1859c_auto-bluetooth-pairing.md) | Cursor — Wii PIN pairing |
| 8 | 2026-07-07 14:00:00 | [`5256356`](https://github.com/Vabian124/balance-board-controller/commit/5256356) | [UI redesign](2026-07-07_140000_5256356_ui-redesign.md) | Cursor — system themes, scripts |
| 9 | 2026-07-07 15:00:00 | [`4942277`](https://github.com/Vabian124/balance-board-controller/commit/4942277) | [Smart connect workflow](2026-07-07_150000_smart-connect-workflow.md) | Cursor — edge cases, test plan |
| 10 | 2026-07-07 15:15:00 | [`a5360f4`](https://github.com/Vabian124/balance-board-controller/commit/a5360f4) | [Persistence and logs](2026-07-07_151500_persistence-and-logs.md) | Cursor — settings, connection state |
| 11 | 2026-07-07 15:35:00 | [`ef0021e`](https://github.com/Vabian124/balance-board-controller/commit/ef0021e) | [Final lint and push](2026-07-07_153500_final-lint-and-push.md) | Cursor — format, Python porting doc |
| 12 | 2026-07-07 15:45:00 | [`4fdb527`](https://github.com/Vabian124/balance-board-controller/commit/4fdb527) | [Fix CornerRadius crash](2026-07-07_154500_fix-cornerradius-crash.md) | Cursor — XAML theme bug |
| 13 | 2026-07-07 15:55:00 | [`b74c488`](https://github.com/Vabian124/balance-board-controller/commit/b74c488) | [Lint and UI smoke](2026-07-07_155500_lint-and-ui-smoke.md) | Cursor — static analysis |
| 14 | 2026-07-07 16:00:00 | [`1aa734c`](https://github.com/Vabian124/balance-board-controller/commit/1aa734c) | [Python port prep refactor](2026-07-07_160000_python-port-prep.md) | Cursor — BalanceMath, tests |
| 15 | 2026-07-07 16:10:00 | [`ff60cb2`](https://github.com/Vabian124/balance-board-controller/commit/ff60cb2) | [Crash logging hardening](2026-07-07_151500_crash-logging.md) | Cursor — global exception logs |

## Quick HEAD check

```powershell
git rev-parse --short HEAD
git log -1 --format="%ci %s"
```

If `HEAD` does not match the latest index row, read commits after that row with `git log <latest-known-hash>..HEAD`.

## Related docs

- [AGENTS.md](../../AGENTS.md) — how to work in this repo
- [INSTRUCTIONS.md](../../INSTRUCTIONS.md) — every-pass commit & maintenance rules
- [ROADMAP.md](../ROADMAP.md) — planned future work (not done until an update entry says done)
- [ARCHITECTURE.md](../ARCHITECTURE.md) — current system design
