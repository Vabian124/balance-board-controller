# Update 016 — Connection flow logging and multi-HID fix

| Field | Value |
|-------|-------|
| Commit | *(set after commit)* |
| Date | 2026-07-07 |
| Branch | `main` |

## What was done

- **`[CONNECT]` log markers** throughout pair/HID/connect/disconnect flow
- **`ConnectionFlowLogger`** + unit tests
- **Multi-HID scan** — tries all Wii devices until a balance board is found
- **Crash-safe log writes** — `FileOptions.WriteThrough` on each line
- **Status changes** written to session log from UI
- **Shutdown / dispose** exceptions logged
- **First poll reading** logged after connect

## Static analysis

`lint.ps1`: build 0 warnings, 22 unit tests, Validate, UI smoke, lifecycle — all passed.

HID validate currently shows no Wii devices (board may be paired at BT layer but not visible to WiimoteLib until SYNC).
