# Agent update log

Short index for AI assistants. User-facing history: [CHANGELOG.md](../../CHANGELOG.md). Git is the source of truth for detailed history.

## Recent (newest first)

| Commit | Summary |
|--------|---------|
| *pending* | **v1.4.0** — unified `scripts/ci/test.ps1` pipeline, headless WPF UI tests, hang hardening (worker/dispatcher/test watchdogs), `--physical-test connect-basic` hardware lane |
| [`aacec1d`](https://github.com/Vabian124/balance-board-controller/commit/aacec1d) | **post-v1.3.0** — skip duplicate connect when session already healthy |
| [`ec7bc23`](https://github.com/Vabian124/balance-board-controller/commit/ec7bc23) | **post-v1.3.0** — vJoy Center() after acquire; nullable split-axis sensitivity persistence + tests |
| [`b62cdba`](https://github.com/Vabian124/balance-board-controller/commit/b62cdba) | **v1.3.0** — vJoy unsigned axis center mapping, per-axis deadzone/sensitivity UI, debug trace cleanup |
| [`29dc308`](https://github.com/Vabian124/balance-board-controller/commit/29dc308) | **v1.2.4** — BT MAC trust, wake-probe crash fix, Fine Tuning tab, hair-trigger sensitivity, HID instance IDs |
| [`fe1bca6`](https://github.com/Vabian124/balance-board-controller/commit/fe1bca6) | Docs: add v1.2.3 agent update log entry. |
| [`cec9107`](https://github.com/Vabian124/balance-board-controller/commit/cec9107) | **Dashboard UI revamp** — collapsible session log (default collapsed, last-line preview), visual-first Dashboard layout, `SessionLogExpanded` setting |
| [`b39e435`](https://github.com/Vabian124/balance-board-controller/commit/b39e435) | Agent log entry for zero-touch BT wake |
| [`a53b292`](https://github.com/Vabian124/balance-board-controller/commit/a53b292) | **WiiBrew protocol** — `BalanceBoardProtocol`, wake/connect `0x34`, extension `0x0402` logs, CONNECTION_PROTOCOL reference |
| [`a59ea89`](https://github.com/Vabian124/balance-board-controller/commit/a59ea89) | **UI + BT adapter** — pinned live log, Fine tuning sliders on Advanced tab, adapter MAC change detection + auto re-pair |
| [`f025900`](https://github.com/Vabian124/balance-board-controller/commit/f025900) | **BT recovery** — truthful HID health, auto-reconnect after radio drop without SYNC, UI reconnecting states |
| [`e971a74`](https://github.com/Vabian124/balance-board-controller/commit/e971a74) | **v1.2.1** — sensitivity slider visibility fix, response curves, one-foot mode |
| [`e3764f1`](https://github.com/Vabian124/balance-board-controller/commit/e3764f1) | **v1.2.0** — version metadata fix, release zip naming aligned with tags |
| [`4eeaa4a`](https://github.com/Vabian124/balance-board-controller/commit/4eeaa4a) | **Docs v1.1.1** — README, CODEMAP, ROADMAP, archive cleanup |
| [`cf6a36a`](https://github.com/Vabian124/balance-board-controller/commit/cf6a36a) | **Finish-line** — crash-proof disconnect/Minecraft, jump presets, UI detail levels, structured logs |
| [`543455f`](https://github.com/Vabian124/balance-board-controller/commit/543455f) | Tabbed UI, dark-mode ComboBox contrast fix |
| [`d7f5f7d`](https://github.com/Vabian124/balance-board-controller/commit/d7f5f7d) | **v1.1.0** — sensitivity presets, dark mode, action mapping UI, balance visual, mouse mode |
| [`5bfb026`](https://github.com/Vabian124/balance-board-controller/commit/5bfb026) | **Connect fix** — removed `NotImplementedException` stubs from `BeginConnect` |
| [`aebfc01`](https://github.com/Vabian124/balance-board-controller/commit/aebfc01) | **ConnectionWorker** STA thread, test pyramid, crash hardening |

Older granular session notes were removed in the docs cleanup (Jul 2026); use `git log` for full history.

## After you commit

Add one line to the table above (commit hash + one sentence). Do not recreate dozens of separate files unless the change is large.

## Quick check

```powershell
git rev-parse --short HEAD
git log -5 --oneline
```
