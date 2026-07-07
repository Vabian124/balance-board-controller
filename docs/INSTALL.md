# Installation guide

Step-by-step setup for **end users** who want to run Balance Board Controller on Windows. Developers: see [DEVELOPMENT.md](DEVELOPMENT.md).

## What you need

| Requirement | Notes |
|-------------|--------|
| **Windows 10/11 (64-bit)** | Bluetooth required for the board |
| **[.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)** | Required to run the app (SDK only needed if building from source) |
| **[vJoy driver](https://github.com/shauleiz/vJoy)** | Install, reboot, enable **Device 1** with X/Y axes in vJoyConf |
| **Wii Fit Balance Board** | Pair via the app on first connect (press SYNC) |

## Option A — Build from source (recommended)

Building from the public GitHub repo is the most transparent way to run the app and avoids downloading unsigned binaries from strangers.

```powershell
git clone https://github.com/Vabian124/balance-board-controller.git
cd balance-board-controller
.\build.bat
.\start.bat
```

Or with PowerShell:

```powershell
dotnet build BalanceBoard.sln -c Release
.\scripts\dev\start.ps1   # dev mode (allows multiple instances)
```

**Output:** `src\BalanceBoard.App\bin\Release\net8.0-windows\BalanceBoardApp.exe`

## Option B — GitHub Release zip

When a [GitHub Release](https://github.com/Vabian124/balance-board-controller/releases) is published:

1. Download `BalanceBoardController-*-win-x64.zip` and the matching `.sha256` file.
2. Verify the checksum (optional but recommended):

   ```powershell
   Get-FileHash .\BalanceBoardController-1.0.0-win-x64.zip -Algorithm SHA256
   # Compare to contents of the .sha256 file
   ```

3. Extract to a folder (e.g. `C:\Apps\BalanceBoardController\`).
4. Install vJoy and .NET 8 Desktop Runtime if not already installed.
5. Run `BalanceBoardApp.exe`.

Releases are built by [GitHub Actions](https://github.com/Vabian124/balance-board-controller/actions) from the tagged source — no third-party repacks.

## First run

1. Launch the app (`start.bat` or `BalanceBoardApp.exe`).
2. Click **Connect**.
3. Press the **red SYNC** button under the board (first pairing).
4. Stand on the board → click **Tare**.
5. Open **vJoy Monitor** or your game to confirm stick movement.

Session logs (for support): `%AppData%\BalanceBoardApp\logs\session-YYYY-MM-DD.log`

## Antivirus and Windows SmartScreen

This app is **legitimate open-source software** ([MIT license](../LICENSE), full source on GitHub). It is **not** malware, but some security products may still flag it because:

| Behavior | Why it exists |
|----------|----------------|
| **Virtual joystick (vJoy)** | Standard gaming/accessibility pattern — same family as JoyToKey, reWASD, etc. |
| **Keyboard/mouse simulation (`SendInput`)** | Optional “hand-free desktop” preset — only when you enable that mode |
| **Bluetooth / HID access** | Required to read the balance board |
| **Unsigned executable** | No code-signing certificate (costly for hobby OSS). **Build from source** if you prefer. |

**What we do *not* do:** no network downloads at runtime, no remote control servers, no credential harvesting, no obfuscation/packing, no admin elevation (`asInvoker` manifest).

**If Windows SmartScreen warns:** click “More info” → “Run anyway”, or build from source. Submitting the open repo to your AV vendor as a false positive also helps the community.

**If your AV quarantines DLLs:** restore from quarantine and add an exclusion for the app folder, or build locally.

## Troubleshooting

| Problem | Fix |
|---------|-----|
| “You must install .NET” | Install [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) |
| vJoy not found | Reboot after vJoy install; check Device Manager |
| Connect crash | Update to latest `main`; see session log |
| DLL version mismatch | Copy `vJoyInterface*.dll` from your vJoy install into `libs/x64/` and rebuild |

More: [README.md](../README.md#troubleshooting)

## Uninstall

1. Delete the app folder.
2. Optionally remove `%AppData%\BalanceBoardApp\` (settings + logs).
3. Uninstall vJoy separately if you no longer need it (Windows Settings → Apps).
