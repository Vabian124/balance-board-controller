# Update 002 — Profile presets, input improvements, CI

| Field | Value |
|-------|-------|
| **Date** | 2026-07-07 13:10:44 +0200 (UTC+2) |
| **Short commit** | `fb82a5d` |
| **Full commit** | `fb82a5d6431a48e7dfbbafacf1aa762944ca10d6` |
| **Branch** | `main` |
| **Parent** | `4a0b399` |
| **GitHub** | https://github.com/Vabian124/balance-board-controller/commit/fb82a5d |
| **Agent context** | Cursor AI — finish todos, lint/clean/maintain, push to GitHub |

## Summary

Added **ActionPresets**, quick-start profile UI, X1/X2 mouse buttons, movement trigger sliders, auto-connect, CI workflow, and editorconfig/build props. Pushed to `origin/main`.

## What was done

### Models & session
- **`ActionPresets.cs`** — Game Controller, Pedal / Rudder, Hand-Free Desktop, Default
- **`BalanceBoardSession`** — `ApplyProfile()`, `ApplyKeyboardPreset()`, presets delegate to `ActionPresets`
- **`AppSettings`** — default triggers restored to legacy values (8, 9, 15, 16)

### Input
- **`InputSimulator`** — X1/X2 (back) mouse buttons via `MOUSEEVENTF_XDOWN/XUP`
- Fixed `INPUT_KEYBOARD` constant for key events

### UI (`MainWindow`)
- Quick Start card: profile `ComboBox`, Play Games / Hand-Free Desktop / Pedals buttons
- Movement trigger sliders (left/right, forward/back)
- Auto-connect on startup checkbox
- Active input display on dashboard
- `SyncUiFromSettings()` — keeps UI aligned after preset changes
- `Window_Loaded` — auto-connect when enabled

### Maintenance
- `.editorconfig`, `Directory.Build.props`
- `.github/workflows/build.yml` — Windows CI: restore, Release build, Validate tool
- `BalanceBoard.Validate` added to `BalanceBoard.sln`
- `dotnet format` run; Release build 0 warnings
- `README.md` — profiles and quick-start wording

## What was NOT done (still future)

- Full per-action mapping editor UI
- `SettingsStore` profile save/load UI (API exists, no UI)
- Tray icon + `StartMinimized`
- Multi-device picker dialog
- Accessibility pass (AutomationProperties, large UI)
- `WiiBalanceWalker.sln` left untracked locally

## Files changed (13 files, +412 / −58)

| Area | Files |
|------|-------|
| New | `ActionPresets.cs`, `.editorconfig`, `Directory.Build.props`, `.github/workflows/build.yml` |
| UI | `MainWindow.xaml`, `MainWindow.xaml.cs` |
| Core | `AppSettings.cs`, `BalanceBoardSession.cs`, `InputSimulator.cs` |
| Other | `BalanceBoard.sln`, `README.md`, `App.xaml.cs`, `tools/Validate/Program.cs` |

## Verify at this commit

```powershell
git checkout fb82a5d
dotnet build BalanceBoard.sln -c Release
dotnet run --project tools/Validate/BalanceBoard.Validate.csproj -c Release
```

## Notes for future agents

- Preset logic lives in **`ActionPresets`** — do not duplicate in UI handlers; use `ApplyProfile` / `SyncUiFromSettings`.
- CI validate step uses `dotnet run --no-restore` (builds Validate if needed).
- Items in ROADMAP were not started at this commit.
