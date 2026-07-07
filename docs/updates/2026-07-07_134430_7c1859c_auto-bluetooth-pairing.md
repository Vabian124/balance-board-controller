# Update 007 — Automatic Bluetooth pairing

| Field | Value |
|-------|-------|
| **Date** | 2026-07-07 13:44:30 +0200 (UTC+2) |
| **Short commit** | `7c1859c` |
| **Full commit** | `7c1859c` *(see `git rev-parse 7c1859c`)* |
| **Branch** | `main` |
| **Agent context** | User requested no manual Windows Bluetooth pairing — WiiBalanceWalker MAC PIN method |

## Summary

Automatic Wii Balance Board pairing using reversed host Bluetooth MAC as permanent PIN (WiiBalanceWalker). No Windows Bluetooth menus or PIN 0000 popups. Auto-connect on every app launch.

## What was done

- `BluetoothPairingService`, `WiiBluetoothPin`, `StaThread`
- `BalanceBoardSession.ConnectOrPair()` — discover, pair, connect
- Startup auto-connect; removed blocking MessageBox on connect failure
- Setup Wizard updated for automatic pairing flow
- InTheHand.Net.Personal wired into Core + App output

## What was NOT done

- User must still press physical SYNC once (board hardware requirement)
- MAC addresses containing `00` cannot use permanent PIN (documented in logs)

## Notes

- Only action needed: press red SYNC when status says "Searching for balance board"
- Logs: `%AppData%\BalanceBoardApp\logs\`
