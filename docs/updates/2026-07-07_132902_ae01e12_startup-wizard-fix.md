# Update 006 — Startup fixes and first-run wizard

| Field | Value |
|-------|-------|
| **Date** | 2026-07-07 13:29:02 +0200 (UTC+2) |
| **Short commit** | `ae01e12` |
| **Full commit** | `ae01e12befa58c8b5c4f6db388a38504761ff79e` |
| **Branch** | `main` |
| **Parent** | `74359d7` |
| **Agent context** | Cursor — user first launch; fix startup race, vJoy logging, auto wizard |

## Summary

Fixed deferred `ProfileCombo` startup race (`_uiReady` moved to `Loaded`), added vJoy startup logging, `SetupWizardCompleted` flag, and auto-open Setup Wizard for new users.

## What was done

- `MainWindow` — `_uiReady` only after `Loaded`; startup vJoy diagnostic log lines
- `AppSettings.SetupWizardCompleted` — wizard auto-shows until finished
- `SettingsStore.HasPersistedSettings` / `SettingsPath` properties
- Launched app for user; vJoy device 1 acquired; session log confirmed

## What was NOT done

- Balance board not paired yet (user must complete wizard / Windows Bluetooth)
- DLL version 0x219 vs 0x218 warning remains (functional)

## Notes for future agents

- Session logs: `%AppData%\BalanceBoardApp\logs\session-YYYY-MM-DD.log` (automatic)
- vJoy was `VJD_STAT_OWN` after acquire — expected while app runs
