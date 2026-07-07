# Agent update log

Short index for AI assistants. User-facing history: [CHANGELOG.md](../../CHANGELOG.md). Git is the source of truth for detailed history.

## Recent (newest first)

| Commit | Summary |
|--------|---------|
| [`7aed8ee`](https://github.com/Vabian124/balance-board-controller/commit/7aed8ee) | **WiiBrew protocol** — CONNECTION_PROTOCOL.md, `BalanceBoardProtocol` wake/connect `0x34` + extension id `0x0402` logging |
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
