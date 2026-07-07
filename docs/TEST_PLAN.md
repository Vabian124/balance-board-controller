# Test plan — boot, connect, and edge cases

Use this matrix to verify workflow behavior. Hardware tests need a Wii Balance Board + Bluetooth + vJoy; smoke tests run without hardware.

## Automated smoke tests (no board)

```powershell
cd <repo-root>
.\scripts\test-flow.ps1
```

Checks: build, start/stop lifecycle, second-instance activation, log file creation.

## Edge-case matrix

| # | Scenario | Setup | Steps | Expected |
|---|----------|-------|-------|----------|
| 1 | **Cold first launch** | Delete `%AppData%\BalanceBoardApp\settings.json` | Run app | Window &lt; 1s; "Welcome — click Connect"; **no** BT search in log |
| 2 | **First connect** | Scenario 1 | Click Connect, press SYNC | Pairing in log; connects; `HasConnectedBefore` saved |
| 3 | **Return + board on** | After #2, board on | Restart app | Quick reconnect; connected &lt; 2s; **no** "Removing stale Nintendo" |
| 4 | **Return + board off** | Paired, board powered off | Restart app | "Board offline…"; app idle; no hang |
| 5 | **Wake asleep board** | #4 state | Turn board on, press SYNC, Connect | Light pairing round or HID connect; no full unpair first |
| 6 | **Cancel during search** | Click Connect | Cancel mid-search | "Cancelled"; Connect enabled; no freeze |
| 7 | **Exit during connect** | Click Connect | Exit | Process exits; no zombie `BalanceBoardApp` |
| 8 | **Second instance** | App running | Launch exe again | Second exits; first window foreground; log "brought to front" |
| 9 | **Dev multiple** | `.\scripts\start.ps1` twice | Two windows | Both run (`--dev`) |
| 10 | **`--connect` script** | `.\scripts\connect.ps1` | Full pairing starts | Log: "Launch flag --connect" |
| 11 | **Auto-connect off** | Uncheck setting, restart | No reconnect attempt | Idle until Connect |
| 12 | **vJoy missing** | Uninstall vJoy | Launch | vJoy chip warning; no crash |
| 13 | **vJoy busy** | Hold device in another app | Launch | Status shows BUSY; health check explains |
| 14 | **stop.ps1 while connecting** | Connect in progress | `.\scripts\stop.ps1` | Process stopped |
| 15 | **Light/dark theme** | Switch Windows theme | Open app | Text readable on all chips/cards |
| 16 | **Disconnect** | Connected | Disconnect | Poll stops; vJoy centered |
| 17 | **Reconnect after disconnect** | Disconnected | Connect | Works (full or quick per button) |
| 18 | **Settings persist** | Change deadzone | Restart | Value restored |
| 19 | **Legacy settings** | `SetupWizardCompleted: true` only | Load | `HasConnectedBefore` migrated |
| 20 | **Health check** | Any | Run health check | Report in log; copy works |

## Manual test procedure

### 1. First-time user (zero state)

```powershell
Remove-Item "$env:APPDATA\BalanceBoardApp\settings.json" -ErrorAction SilentlyContinue
dotnet run --project src/BalanceBoard.App/BalanceBoard.App.csproj -c Release
```

- [ ] Window appears immediately
- [ ] Status: welcome message
- [ ] Session log has **no** "Searching for balance board" until Connect clicked

### 2. Pairing

- [ ] Click Connect → SYNC prompt
- [ ] Board pairs without Windows PIN dialog
- [ ] Connected chip green; weight updates when stepping on

### 3. Returning user fast path

- [ ] Close app, reopen (board still on)
- [ ] Auto-reconnect without pairing spam
- [ ] Log contains "Auto-reconnect" not "Removing stale Nintendo"

### 4. Cancellation

- [ ] Connect with board off → Cancel within 5s
- [ ] UI responsive; can Connect again

### 5. Single instance

```powershell
Start-Process ".\src\BalanceBoard.App\bin\Release\net8.0-windows\BalanceBoardApp.exe"
Start-Sleep 2
Start-Process ".\src\BalanceBoard.App\bin\Release\net8.0-windows\BalanceBoardApp.exe"
```

- [ ] Only one process remains
- [ ] First window activated

## Regression checklist (every release)

```powershell
dotnet build BalanceBoard.sln -c Release
.\scripts\test-flow.ps1
```

- [ ] Build succeeds
- [ ] Smoke script passes
- [ ] No unhandled exceptions in `%AppData%\BalanceBoardApp\logs\`

## What automation cannot cover

- Real Bluetooth pairing timing and SYNC window
- vJoy interaction with specific games
- Multiple Nintendo devices in range

Document manual results in PR test plan or a dated note under `docs/updates/`.
