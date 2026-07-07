# Balance Board Controller

A modern .NET 8 desktop app for using a **Nintendo Wii Fit Balance Board** on Windows as a **game controller** (via [vJoy](https://github.com/shauleiz/vJoy)) or as a hand-free input device.

Built as a clean rewrite of [WiiBalanceWalker v0.5](https://github.com/lshachar/WiiBalanceWalker) with an updated UI, automatic Bluetooth pairing, diagnostics, and safer process handling.

> **AI assistants / coding agents:** read [INSTRUCTIONS.md](INSTRUCTIONS.md) (every-pass checklist), then [AGENTS.md](AGENTS.md) and [llms.txt](llms.txt). Full docs live in [`docs/`](docs/).

## Features

- **Modern dashboard** — live balance visual, direction text, connection status pills
- **Game controller mode** — maps lean to vJoy X/Y axes (works with most games that accept joysticks)
- **Hand-free desktop preset** — WASD + Shift + Space + mouse nudge (legacy WiiBalanceWalker bindings)
- **Pedal / rudder preset** — maps four load sensors to extra vJoy axes
- **Quick-start profiles** — dropdown + one-click presets on the Dashboard
- **Smart connect** — first launch waits for you; returning users auto-reconnect without re-pairing
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
3. **Launch the app** — easiest way:

   **Double-click `start.bat`** in the project folder.

   Or from PowerShell:

   ```powershell
   .\start.bat
   # or
   .\scripts\start.ps1
   ```

4. **First run:** click **Connect** and press the red **SYNC** button on the board when prompted.
5. **Returning users:** the app reconnects automatically when the board is on (toggle in settings).
6. Stand on the board → **Tare** if needed. Verify axes in **vJoy Monitor**.

### Build from source (optional)

```powershell
git clone https://github.com/Vabian124/balance-board-controller.git
cd balance-board-controller
dotnet build BalanceBoard.sln -c Release
dotnet run --project src/BalanceBoard.App/BalanceBoard.App.csproj -c Release
```

### Publish (single folder)

```powershell
dotnet publish src/BalanceBoard.App/BalanceBoard.App.csproj -c Release -r win-x64 --self-contained
```

### Dev scripts (start / stop / restart)

```powershell
.\start.bat              # double-click or run from repo root
.\stop.bat               # stop the app
.\scripts\start.ps1      # same as start.bat (PowerShell)
.\scripts\stop.ps1       # graceful exit, then force stop
.\scripts\restart.ps1    # stop + start
.\scripts\connect.ps1    # start and auto-connect
.\scripts\test-flow.ps1  # smoke tests (no hardware)
.\scripts\lint.ps1       # full lint + UI XAML smoke
```

See [docs/WORKFLOW.md](docs/WORKFLOW.md) and [docs/TEST_PLAN.md](docs/TEST_PLAN.md) for boot/connect behavior and edge-case testing.

Use `--dev` to skip killing other instances. Use `--connect` to force full pairing on launch.

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

- **Balance Board Controller** (new code in `src/`, `tools/`, and project docs): **[MIT License](LICENSE)** — free to use, fork, modify, and redistribute with minimal restrictions.
- **WiiBalanceWalker** (legacy reference under `WiiBalanceWalker/`): Microsoft Public License — see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)

You may fork this repo and do what you want with the MIT-licensed portions. Keep the LICENSE file and third-party notices when you redistribute.

## Acknowledgements

- [WiiBalanceWalker](https://github.com/lshachar/WiiBalanceWalker) by Shachar Liberman / Richard Perry
- [WiimoteLib](https://github.com/lshachar/WiimoteLib)
- [vJoy](https://github.com/shauleiz/vJoy) by Shaul Eizikovich
