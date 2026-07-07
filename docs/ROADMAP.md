# Roadmap

Planned improvements. Safe for agents to implement incrementally.

## Phase 2 — UX & accessibility

- [ ] **Action mapping editor** — UI for all 8 `ActionBinding` slots (key picker, mouse button including X1/X2, move amount)
- [ ] **Game profiles UI** — wire `SettingsStore.SaveProfile` / `LoadProfile` / `ListProfiles` to dropdown + save/load buttons
- [ ] **Multi-device picker** — when `DiscoverDevices()` returns >1, show selection dialog before `Connect(index)`
- [ ] **Tray icon** — minimize to tray, quick connect/disconnect
- [ ] **Start minimized** — honor `AppSettings.StartMinimized`
- [ ] **Accessibility pass** — `AutomationProperties`, live regions for direction text, optional large UI mode

## Phase 3 — robustness

- [ ] Unit tests for `BalanceProcessor` (triggers, deadzone, center offset)
- [ ] Integration test mock for `BalanceBoardConnection`
- [ ] Configurable poll rate in settings
- [ ] Graceful reconnect when Bluetooth drops

## Phase 4 — distribution

- [ ] GitHub Releases with published `win-x64` self-contained zip
- [ ] Installer (MSIX or Inno Setup)
- [ ] Auto-update check (optional)

## Implementation hints

| Item | Start in |
|------|----------|
| Action mapping UI | New `ActionEditorWindow.xaml`, bind to `AppSettings.Actions` |
| Profiles UI | `MainWindow` + `SettingsStore.ListProfiles()` |
| Device picker | Modal before `ConnectButton_Click` |
| Tray | `Hardcodet.NotifyIcon.Wpf` or WinForms `NotifyIcon` in `App.xaml.cs` |

When implementing, update this file (check off items) and add a line to `AGENTS.md` if architecture changes.
