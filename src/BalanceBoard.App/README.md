# BalanceBoard.App

WPF desktop application (`BalanceBoardApp.exe`).

## Responsibilities

- Dashboard UI (live balance, profiles, calibration)
- Setup wizard
- Debug Suite (health check, logs)
- Theme and visual controls

## Entry point for agents

See [../../AGENTS.md](../../AGENTS.md). UI rules: [../../.cursor/rules/wpf-ui.mdc](../../.cursor/rules/wpf-ui.mdc).

**Main window:** `MainWindow.xaml(.cs)`  
**Startup:** `App.xaml.cs` (single instance, feeder cleanup)

## Run

```powershell
dotnet run --project src/BalanceBoard.App/BalanceBoard.App.csproj -c Release
```
