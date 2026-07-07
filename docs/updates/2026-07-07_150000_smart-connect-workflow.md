# Update 009 — Smart connect workflow and edge-case hardening

| Field | Value |
|-------|-------|
| Commit | *(set after commit)* |
| Date | 2026-07-07 |
| Branch | `main` |

## What was done

- **`ConnectionIntent`**: `QuickReconnect` (HID only) vs `PairAndConnect` (full pairing)
- **First launch**: welcome message; no automatic Bluetooth search until user clicks Connect
- **Returning users**: `AutoConnectOnStartup` (default on) runs quick reconnect only
- **`HasConnectedBefore`** setting + migration from legacy `SetupWizardCompleted`
- **Pairing**: light SYNC attempt without unpairing before full pairing rounds
- **Deferred vJoy init** — constructor no longer blocks on vJoy acquire
- **`SingleInstanceService`**: second launch brings existing window to front (named pipe)
- **Docs**: `docs/WORKFLOW.md`, `docs/TEST_PLAN.md`
- **Script**: `scripts/test-flow.ps1` — automated smoke tests (build, start/stop, single-instance)

## Verified

- `dotnet build -c Release` — OK
- `.\scripts\test-flow.ps1` — all smoke tests passed

## Not done / left for later

- Hardware regression on real board (manual matrix in TEST_PLAN.md)
- Pairing discovery timeout tuning per adapter
- Tray icon / minimize-to-tray
