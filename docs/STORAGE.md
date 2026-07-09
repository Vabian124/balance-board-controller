# User data and persistence

All runtime data lives under **`%AppData%\BalanceBoardApp\`** (Windows: `C:\Users\<you>\AppData\Roaming\BalanceBoardApp\`). Nothing in this folder is committed to git.

Code reference: `AppDataPaths`, `SettingsStore`, `FileLogService`.

## Layout

```
%AppData%\BalanceBoardApp\
├── settings.json          # Main configuration + connection history
├── settings.json.tmp      # Atomic write temp (deleted after save)
├── profiles\              # Optional saved profile snapshots (future UI)
│   └── MyProfile.json
└── logs\
    └── session-YYYY-MM-DD.log   # One file per calendar day, append-only
```

## settings.json

Written by `SettingsStore.Save()` using **atomic replace** (write `.tmp`, then move) so a crash mid-save does not corrupt the file.

| Field | Purpose |
|-------|---------|
| `TriggerLeftRight`, `TriggerForwardBackward`, … | Balance / input tuning |
| `EnableVJoy`, `VJoyDeviceId` | Virtual joystick output |
| `ActiveProfileName`, `Actions` | Preset and bindings |
| `UiDetailLevel` | Simple / Standard / Advanced — tab and panel visibility |
| `JumpLevel` | Easy / Normal / Hard — jump threshold bundle |
| `ThemeMode` | System / Light / Dark appearance |
| `AutoConnectOnStartup` | Quick reconnect on launch (returning users) |
| `HasConnectedBefore` | `false` = first-launch welcome; `true` = returning user |
| `LastConnectedDeviceId` | HID device id from last successful connect (e.g. `002331987181`) |
| `LastBluetoothAdapterMac` | Host Bluetooth adapter MAC (12 hex, no colons) used for Wii PIN pairing |
| `LastConnectedAtUtc` | When that connect succeeded |
| `SetupWizardCompleted` | Legacy; migrated to `HasConnectedBefore` |

### When settings are saved

| Event | What is saved |
|-------|----------------|
| Any UI setting change | Full settings via `SaveSettingsFromUi()` |
| Successful connect | `UpdateConnectionState()` — flags + device id + timestamp |
| Load with legacy file | Migrations applied and **persisted immediately** |
| App exit | `ForceShutdown()` → `SaveSettingsFromUi()` if UI was ready |

### Connection state vs Windows Bluetooth

- **Pairing** is handled by Windows Bluetooth stack (permanent Wii PIN). The app does not store a separate pairing database.
- **`LastConnectedDeviceId`** is the Wii HID path id — used for logs, diagnostics, and future “prefer this board” logic.
- If you pair a different board, the next successful connect overwrites `LastConnectedDeviceId`.

## Session logs

Path: `%AppData%\BalanceBoardApp\logs\session-YYYY-MM-DD.log`

- **Append-only** for the day; each app launch writes a `=== Session start ===` block with settings path and connection flags.
- Categories: `SESSION`, `INFO`, structured tags (`[CONNECT]`, `[DISCONNECT]`, `[JUMP]`, `[VJOY]`, `[SETTINGS]`, `[ERROR]`), `FATAL` (unhandled crashes)
- **Clear View** in the UI clears the on-screen log only; the file is kept.
- Open log folder: Debug Suite → **Open Log Folder**, or `explorer %AppData%\BalanceBoardApp\logs`.

### Reading logs for debugging

Look for:

```
[SESSION] Balance Board Controller v1.5.2 (stable) — reference connect release
[SESSION] === Session start ===
[SESSION] Flags: HasConnectedBefore=True, AutoConnectOnStartup=True, ...
[SESSION] Last board: 002331987181 at ...
```

Pairing issues: lines with `Bluetooth adapter`, `Removing stale Nintendo`, `Pairing Nintendo RVL-WBC-01`.

Crashes: search for `[ERROR]` or `FATAL` — unhandled UI, background, and AppDomain exceptions are written with full stack traces before the process exits.

Connection flow: search for `[CONNECT]` — intent, HID discovery, pairing rounds, HID attempts, first reading, and flow complete markers.

Disconnect issues: search for `[DISCONNECT]` — teardown steps and benign callback-drain messages.

## Profiles directory

`SettingsStore.SaveProfile` / `LoadProfile` / `ListProfiles` write JSON snapshots under `profiles\`. The **Profiles** tab uses built-in `ActionPresets` buttons today; save/load UI for custom named profiles is planned ([ROADMAP.md](ROADMAP.md)).

## Reset / first-time simulation

```powershell
# Full reset (settings + logs)
Remove-Item -Recurse -Force "$env:APPDATA\BalanceBoardApp"

# Settings only (keeps logs)
Remove-Item "$env:APPDATA\BalanceBoardApp\settings.json"
```

After reset, next launch behaves as **first launch** (`HasConnectedBefore == false`).

## Related docs

- [WORKFLOW.md](WORKFLOW.md) — boot and connect behavior driven by these flags
- [TEST_PLAN.md](TEST_PLAN.md) — scenarios that verify persistence
