# BalanceBoard.Core

Platform-agnostic logic library (no WPF).

## Responsibilities

- Wii balance board connection (WiimoteLib)
- Balance processing and movement triggers
- vJoy virtual controller output
- Keyboard/mouse simulation (SendInput)
- Settings persistence and diagnostics

## Entry point for agents

See [../../AGENTS.md](../../AGENTS.md) and [../../docs/CODEMAP.md](../../docs/CODEMAP.md).

**Main orchestrator:** `Services/BalanceBoardSession.cs`

## Build

```powershell
dotnet build src/BalanceBoard.Core/BalanceBoard.Core.csproj -c Release
```
