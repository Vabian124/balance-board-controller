# Roadmap

Planned improvements. Safe for agents to implement incrementally.

## Completed (through v1.2.0)

- [x] **Tabbed UI** — Dashboard / Profiles / Advanced (`MainTabControl`)
- [x] **UI detail levels** — Simple / Standard / Advanced (`UiDetailLevel`)
- [x] **Jump presets** — Easy / Normal / Hard (`JumpPresets`)
- [x] **Action mapping editor** — bindings in Advanced tab for all 8 `ActionBinding` slots
- [x] **Dark theme** — System / Light / Dark (`ThemeManager`, `Colors.Light` / `Colors.Dark`)
- [x] **Sensitivity presets** — Low / Medium / High / Highly sensitive
- [x] **Minecraft (Controlify)** profile — vJoy stick + button 1 jump
- [x] **Balance Mouse** profile — lean cursor + jump click
- [x] **Crash-proof disconnect** — hardened WiimoteLib HID teardown
- [x] **Structured log tags** — `[CONNECT]`, `[DISCONNECT]`, `[JUMP]`, `[VJOY]`, `[SETTINGS]`, `[ERROR]`
- [x] **Unit tests** — `BalanceProcessor`, `BalanceMath`, `JumpPresets`, `ActionEngine`, settings migrations
- [x] **Integration tests** — session disconnect, simulated board, late HID callbacks
- [x] **CI quality gate** — `.github/workflows/ci.yml` + `scripts/ci/lint.ps1`
- [x] **GitHub Releases** — `release.yml` publishes `win-x64` zip on version tags

## Phase 2 — UX & accessibility

- [x] **Custom game profiles UI** — "My Profiles" card on the Profiles tab: save-as, load, update, delete, export, and import named snapshots (`SettingsStore` profile APIs + `NamePromptDialog`); profiles are portable (connection identity stripped)
- [ ] **Multi-device picker** — when `DiscoverDevices()` returns >1, show selection dialog before `Connect(index)`
- [ ] **Tray icon** — minimize to tray, quick connect/disconnect
- [x] **Start minimized** — honor `AppSettings.StartMinimized` (toggle on Profiles → Appearance &amp; startup; window launches minimized)
- [ ] **Accessibility pass** — `AutomationProperties`, live regions for direction text, optional large UI mode

## Phase 3 — robustness

- [x] Configurable poll rate in settings — Advanced → Performance slider (10–100 ms); `ConnectionWorker.PollIntervalMs` drives the idle poll cadence, wired via `BalanceBoardSession.LoadSettings`
- [x] Graceful reconnect when Bluetooth drops (auto-retry with backoff)

## Phase 4 — distribution

- [ ] Installer (MSIX or Inno Setup)
- [ ] Auto-update check (optional)

## Implementation hints

| Item | Start in |
|------|----------|
| Custom profiles UI | `MainWindow` Profiles tab + `SettingsStore.ListProfiles()` |
| Device picker | Modal before `BeginConnect` |
| Tray | `Hardcodet.NotifyIcon.Wpf` or WinForms `NotifyIcon` in `App.xaml.cs` |
| BT reconnect | `BalanceBoardSession` + `ConnectionWorker` poll/watchdog |

When implementing, update this file (check off items) and add a line to `docs/updates/README.md` if architecture changes.
