# Testing

Automated and manual verification for Balance Board Controller.

## Pyramid

| Layer | Project / script | Needs hardware |
|-------|------------------|----------------|
| Unit | `tests/BalanceBoard.Core.Tests` | No |
| Integration | `tests/BalanceBoard.Integration.Tests` + `BalanceBoard.Testing` fakes | No |
| Fuzz | `tests/BalanceBoard.Fuzz.Tests` (FsCheck) | No |
| Automation | `tests/BalanceBoard.Automation` (spawns exe, `--simulate-board`) | No |
| Hardware | `tests/hardware/Run-HardwareConnect.ps1` | Yes (Wii board) |

## Commands

```powershell
# Full quality gate (same as CI)
.\scripts\lint.ps1

# All tests + optional hardware
.\scripts\ci\test-all.ps1 -IncludeHardware

# Meta: verify test projects are wired in the solution
.\scripts\ci\verify-tests.ps1

# Lifecycle smoke only (start/stop/single-instance)
.\scripts\dev\test-flow.ps1
```

## CI

GitHub Actions workflow [`.github/workflows/ci.yml`](../.github/workflows/ci.yml) runs `scripts/ci/lint.ps1` on every push/PR to `main`.

## Simulate board (no hardware)

```powershell
dotnet run --project src/BalanceBoard.App/BalanceBoard.App.csproj -c Release -- --simulate-board --dev --auto-exit-after 5
```

## Hardware connect check

```powershell
.\tests\hardware\Detect-Board.ps1
.\tests\hardware\Run-HardwareConnect.ps1
```

Expect `[CONNECT] First balance reading` in the session log and no `FATAL` lines after connect.
