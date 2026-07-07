# Python porting guide

> For an AI or developer rewriting this app in Python while preserving behavior.

This document maps the **current .NET implementation** to portable concepts. Read [ARCHITECTURE.md](ARCHITECTURE.md), [WORKFLOW.md](WORKFLOW.md), and [STORAGE.md](STORAGE.md) first.

## What to preserve (behavior contract)

| Behavior | Source of truth |
|----------|-----------------|
| 50 ms poll loop | `BalanceBoardSession` |
| Balance math (tare, center, deadzone, triggers) | `BalanceProcessor.cs` |
| vJoy axis mapping | `VJoyController.cs` + `ActionPresets.cs` |
| Keyboard/mouse bindings | `InputSimulator.cs` |
| Wii BT permanent PIN (reversed host MAC) | `WiiBluetoothPin.cs`, `BluetoothPairingService.cs` |
| Quick reconnect vs full pairing | `ConnectionIntent`, `MainWindow.RunDeferredStartup` |
| Settings file location & fields | `AppSettings.cs`, `STORAGE.md` |
| User data paths | `%AppData%\BalanceBoardApp\` |

**Recommendation:** Port `BalanceBoard.Core` first as a headless library; add UI (PyQt, Tk, or web) second.

## Module map (.NET → Python)

| .NET | Responsibility | Python notes |
|------|----------------|--------------|
| `BalanceBoardConnection` | WiimoteLib HID read | No direct WiimoteLib port. Options: `pywiimote` (if maintained), ctypes to WiimoteLib.dll, or `hidapi` + reverse-engineered report parsing |
| `BluetoothPairingService` | Win32 BT pairing + PIN | `bleak` does not cover classic BT pairing. Use `pywin32`, `windows-curses` BT APIs, or shell to `InTheHand`/custom ctypes. PIN logic is pure — copy from `WiiBluetoothPin.cs` |
| `BalanceProcessor` | Pure math | **Straight port** — no platform deps |
| `VJoyController` | vJoy driver | `pyvjoy` or ctypes to `vJoyInterface.dll` in `libs/x64/` |
| `InputSimulator` | `SendInput` | `pyautogui`, `pynput`, or `ctypes` `user32.SendInput` |
| `BalanceBoardSession` | Orchestrator | Threading: `threading.Timer` or async loop |
| `SettingsStore` | JSON settings | `json` + same paths as `AppDataPaths` |
| `FileLogService` | Daily logs | `logging` module, same filename pattern |
| `MainWindow` | WPF UI | PyQt6 / CustomTkinter / etc. |
| `SingleInstanceService` | Mutex + pipe | `portalocker` or win32 mutex + `win32pipe` |

## Settings JSON (keep compatible)

Path: `%AppData%\BalanceBoardApp\settings.json`

Load/save the same keys so users can switch between .NET and Python builds during development. See `AppSettings.cs` and [STORAGE.md](STORAGE.md).

Critical workflow fields:

- `HasConnectedBefore` — gates first-launch vs auto-reconnect
- `AutoConnectOnStartup` — quick reconnect only (not full pairing)
- `LastConnectedDeviceId`, `LastConnectedAtUtc` — connection history

## Bluetooth pairing algorithm (must match)

From `WiiBluetoothPin.cs` (WiiBalanceWalker method):

1. Read host Bluetooth adapter MAC (12 hex chars, no colons).
2. Reverse byte pairs → 8-digit decimal PIN (e.g. MAC `A8:41:F4:E6:F0:34` → PIN from reversed bytes).
3. During pairing, supply this PIN automatically (no Windows UI).
4. Enable HID service on the Nintendo device after pair.

**Light pairing** (`removeStalePairings: false`) before **full pairing** (remove stale Nintendo devices, up to 4 rounds).

## Data pipeline (copy literally)

```
BalanceReading (4 corners kg, weight, button A)
  → BalanceProcessor.Process(settings)
  → ProcessedBalance (MoveLeft, JoyX/Y, …)
  → VJoyController.Update / InputSimulator.Apply
```

Reference: `BalanceProcessor.cs` — port unit tests against the same numeric fixtures if possible.

## Native DLLs (Windows x64)

Shipped in `libs/x64/`:

| DLL | Use |
|-----|-----|
| `WiimoteLib.dll` | HID Wii/Balance Board (managed, .NET only unless ctypes) |
| `vJoyInterface.dll` | vJoy driver API |
| `vJoyInterfaceWrap.dll` | Managed wrapper |
| `InTheHand.Net.Personal.dll` | Bluetooth pairing (.NET only) |

Python can ctypes-call `vJoyInterface.dll` directly; WiimoteLib is harder — consider keeping a thin .NET helper EXE for HID-only reads if no Python library works.

## Suggested Python project layout

```
balance_board/
  core/
    models.py          # AppSettings, BalanceReading, ProcessedBalance
    processor.py       # BalanceProcessor port
    session.py         # BalanceBoardSession port
    vjoy.py            # VJoyController port
    input_sim.py       # InputSimulator port
    pairing.py         # Wii PIN + Windows BT
    connection.py      # HID / Wiimote
    settings.py        # SettingsStore
    paths.py           # AppDataPaths
  app/
    main.py            # GUI entry
  tools/
    validate.py        # Port of tools/Validate
```

## CLI flags to mirror

| Flag | Behavior |
|------|----------|
| `--dev` | Skip single-instance + feeder cleanup |
| `--connect` | Full pairing on launch |
| `--no-cleanup` | Skip zombie process kill |
| `--allow-multiple` | Allow second instance |

See `StartupOptions.cs`.

## Validation checklist after port

1. `tools/Validate` equivalent — vJoy free, HID discovery, axis sweep
2. `scripts/test-flow.ps1` equivalent — start/stop/single-instance
3. [TEST_PLAN.md](TEST_PLAN.md) matrix — especially first launch, quick reconnect, cancel
4. Compare `ProcessedBalance` outputs against .NET for same synthetic `BalanceReading`

## Files to read in order

1. `src/BalanceBoard.Core/Services/BalanceProcessor.cs`
2. `src/BalanceBoard.Core/Services/BalanceBoardSession.cs`
3. `src/BalanceBoard.Core/Services/WiiBluetoothPin.cs`
4. `src/BalanceBoard.Core/Services/BluetoothPairingService.cs`
5. `src/BalanceBoard.Core/Models/AppSettings.cs`
6. `src/BalanceBoard.App/MainWindow.xaml.cs` (workflow only)
7. `WiiBalanceWalker/` — original MS-PL reference for edge cases

## License note

New code in this repo is **MIT**. `WiiBalanceWalker/` remains **MS-PL**. Python port should include compatible notices if copying algorithms from WiiBalanceWalker.
