# Refactoring for Python port

This document describes how **BalanceBoard.Core** is organized so a future Python rewrite can mirror it module-for-module. No Python code lives in this repo yet.

## Layer map

```
BalanceBoard.Core/
├── Models/              → python/balance_board/models.py
│   ├── AppSettings, BalanceReading, ProcessedBalance
│   ├── ActionSlots, ActionPresets, BalanceConstants, TriggerDefaults
├── Processing/          → python/balance_board/processing/
│   ├── BalanceMath.cs     (pure functions — port first)
│   ├── MovementMapper.cs
│   └── ActionEngine.cs    (portable slot state machine)
├── Abstractions/          → python/balance_board/ports.py
│   ├── IGameControllerOutput
│   ├── IActionSimulator
│   └── IInputBackend
└── Services/              → python/balance_board/adapters/ + session.py
    ├── BalanceProcessor   (thin state wrapper over BalanceMath)
    ├── BalanceBoardSession
    ├── VJoyController     (implements IGameControllerOutput)
    ├── Win32InputBackend  (implements IInputBackend)
    ├── InputSimulator     (facade: ActionEngine + Win32InputBackend)
    ├── BluetoothPairing*  (Windows only)
    └── BalanceBoardConnection (WiimoteLib — Windows only)
```

## Port order (recommended)

1. **`BalanceMath`** + **`BalanceConstants`** — copy literals exactly; run `tests/BalanceBoard.Core.Tests` as golden reference
2. **`ActionSlots`**, **`TriggerDefaults`**, **`ActionPresets`**, **`AppSettings`** — JSON-compatible
3. **`BalanceProcessor`** — stateful tare/center/jump timing
4. **`MovementMapper`** + keyboard binding tables
5. **`IGameControllerOutput`** → pyvjoy adapter
6. **`BalanceBoardSession`** poll loop (50 ms from `BalanceConstants.SessionPollIntervalMs`)
7. Bluetooth/HID adapters last

## DRY changes (this refactor)

| Before | After |
|--------|-------|
| Magic numbers in `BalanceProcessor` | `BalanceConstants` |
| Trigger `8/9/15/16` in 3 places | `TriggerDefaults` |
| 8 hardcoded action slot strings | `ActionSlots` |
| Duplicated vJoy init in session presets | `SyncVJoyFromSettings()` |
| Duplicated `SetAxis` in vJoy | `WriteAxes()` |
| `InputSimulator` 8× `Set(...)` | `MovementMapper` + `ActionEngine` loop |
| Monolithic `Process()` | Delegates to `BalanceMath` |
| WinForms `Keys` in Core | `VirtualKeyCodes` lookup table |
| Monolithic `InputSimulator` | `ActionEngine` + `Win32InputBackend` |
| Session hard-wired adapters | Optional `IGameControllerOutput` / `IActionSimulator` injection |

## Tests as contract

```powershell
dotnet test tests/BalanceBoard.Core.Tests/BalanceBoard.Core.Tests.csproj
```

When porting to Python, reproduce these assertions against the same inputs. See [PYTHON_PORTING.md](PYTHON_PORTING.md).

## Still Windows-coupled (do not port into pure core)

- `Win32InputBackend` — `user32.dll` SendInput
- `VJoyController` — `vJoyInterfaceWrap`
- `BluetoothPairingService`, `BalanceBoardConnection`, `FeederProcessCleanup`
- `ConnectionWorker`, `SingleInstanceService` (in App project)

## Next optional cleanups (not done)

- Neutral rename `JoyZ/Rx/Ry/Rz` → `Sensor*` on `ProcessedBalance` (breaking JSON — defer)
- Extract `SettingsStore` migrations into portable `SettingsMigrations` helpers

See also [ARCHITECTURE.md](ARCHITECTURE.md), [PYTHON_PORTING.md](PYTHON_PORTING.md).
