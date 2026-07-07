# Balance Board Controller

A modern .NET 8 desktop app for using a **Nintendo Wii Fit Balance Board** on Windows as a **game controller** (via [vJoy](https://github.com/shauleiz/vJoy)) or as a hand-free input device.

Built as a clean rewrite of [WiiBalanceWalker v0.5](https://github.com/lshachar/WiiBalanceWalker) with an updated UI, setup wizard, diagnostics, and safer process handling.

> **AI assistants / coding agents:** read [AGENTS.md](AGENTS.md) and [llms.txt](llms.txt) first. Full docs live in [`docs/`](docs/).

## Features

- **Modern dashboard** — live balance visual, direction text, connection status pills
- **Game controller mode** — maps lean to vJoy X/Y axes (works with most games that accept joysticks)
- **Hand-free desktop preset** — WASD + Shift + Space + mouse nudge (legacy WiiBalanceWalker bindings)
- **Pedal / rudder preset** — maps four load sensors to extra vJoy axes
- **Quick-start profiles** — dropdown + one-click presets on the Dashboard
- **Setup wizard** — prerequisites, pairing help, calibration
- **Debug Suite** — one-click health check, session log, copy report, open log folder
- **Safe startup** — automatically stops stale feeder apps that block vJoy
- **File logging** — session logs saved under `%AppData%\BalanceBoardApp\logs\` (never committed to git)

## Requirements

- Windows 10/11 (64-bit)
- [.NET 8 SDK](https://dotnet.microsoft.com/download) (to build) or published self-contained build
- [vJoy driver](https://github.com/shauleiz/vJoy) — reboot after install
- Bluetooth adapter
- Nintendo Wii Fit Balance Board

## Quick start

1. Install **vJoy** and **reboot**.
2. Open **vJoyConf** and enable **Device 1** with at least **X** and **Y** axes.
3. Pair the balance board in **Settings → Bluetooth** (PIN `0000`, hold **SYNC** in the battery bay).
4. Build and run:

```powershell
git clone https://github.com/Vabian124/balance-board-controller.git
cd balance-board-controller
dotnet build BalanceBoard.sln -c Release
dotnet run --project src/BalanceBoard.App/BalanceBoard.App.csproj -c Release
```

5. Pick **Play Games** (or **Hand-Free Desktop** for keyboard/mouse) → connect → **Tare**.
6. Verify axes in **vJoy Monitor** (installed with vJoy).

### Publish (single folder)

```powershell
dotnet publish src/BalanceBoard.App/BalanceBoard.App.csproj -c Release -r win-x64 --self-contained
```

## Debug Suite

Open the **Debug Suite** tab to:

| Action | What it does |
|--------|----------------|
| **Run Health Check** | Tests vJoy, driver match, Wii HID discovery |
| **Copy Report** | Copies diagnostics to clipboard |
| **Open Log Folder** | Opens `%AppData%\BalanceBoardApp\logs\` |
| **Clear View** | Clears the on-screen log (file log is kept) |

CLI alternative:

```powershell
dotnet run --project tools/Validate/BalanceBoard.Validate.csproj -c Release
```

## Project structure

| Path | Description |
|------|-------------|
| `AGENTS.md` | **AI agent onboarding** — architecture, conventions, where to edit |
| `docs/` | CODEMAP, ARCHITECTURE, DEVELOPMENT, GLOSSARY, ROADMAP |
| `src/BalanceBoard.App/` | WPF application (UI, wizard, debug suite) |
| `src/BalanceBoard.Core/` | Board connection, processing, vJoy, logging |
| `tools/Validate/` | Command-line health check tool |
| `libs/x64/` | WiimoteLib + vJoy wrapper DLLs |
| `WiiBalanceWalker/` | Legacy v0.5 reference source (MS-PL) |

## Troubleshooting

| Problem | Fix |
|---------|-----|
| **vJoy busy / file locked** | App auto-stops old instances. Close vJoy Monitor if it holds the device. |
| **vJoy not enabled** | Reboot after install; check Device Manager for *vJoy Device*. |
| **DLL version mismatch** | Copy `vJoyInterface*.dll` from your vJoy install into `libs/x64/`. |
| **Cannot connect** | Remove old Bluetooth pairings; re-pair with SYNC held. |
| **Win11 vJoy install fails** | Try vJoy **2.1.9.1** (community workaround). |

## Logs

Session logs are written to:

```
%AppData%\BalanceBoardApp\logs\session-YYYY-MM-DD.log
```

Log files are excluded from git. They contain timestamps, device status, and connection events only — no personal data.

## License

- **Balance Board Controller** (new code): [MIT License](LICENSE)
- **WiiBalanceWalker** (legacy reference): Microsoft Public License — see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)

## Acknowledgements

- [WiiBalanceWalker](https://github.com/lshachar/WiiBalanceWalker) by Shachar Liberman / Richard Perry
- [WiimoteLib](https://github.com/lshachar/WiimoteLib)
- [vJoy](https://github.com/shauleiz/vJoy) by Shaul Eizikovich
