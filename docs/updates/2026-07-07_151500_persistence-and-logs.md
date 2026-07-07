# Update 010 — Settings, logs, and connection persistence

| Field | Value |
|-------|-------|
| Commit | *(set after commit)* |
| Date | 2026-07-07 |
| Branch | `main` |

## What was done

- Reviewed `session-2026-07-07.log` — found pairing to `002331987181` but no persisted connection state
- **`LastConnectedDeviceId`** + **`LastConnectedAtUtc`** in `AppSettings`; saved on successful connect
- **`SettingsStore.UpdateConnectionState()`** — single place for post-connect persistence
- **Atomic settings save** (`.tmp` then move); **migration persisted on load** (`HasConnectedBefore` from legacy wizard flag)
- **`AppDataPaths`** — canonical `%AppData%\BalanceBoardApp\` layout
- **`FileLogService.WriteSessionHeader()`** — each launch logs settings path, flags, last board
- **`docs/STORAGE.md`** — full reference for settings, logs, profiles, reset
- **`start.bat`** / **`stop.bat`** at repo root for double-click launch

## Not done / left for later

- Auto-prefer last device index when multiple boards visible
- Profile save/load UI
