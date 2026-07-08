# Developer API — using BalanceBoard.Core as a library

`BalanceBoard.Core` is a **.NET 8 class library with no WPF dependency**. Reference it from your own app to consume live balance-board readings and lean without shipping the full desktop UI.

## Reference the project

```powershell
dotnet add YourApp.csproj reference path/to/balance-board-controller/src/BalanceBoard.Core/BalanceBoard.Core.csproj
```

## Minimal session (readings + lean)

```csharp
using BalanceBoard.Core.Models;
using BalanceBoard.Core.Services;

using var session = new BalanceBoardSession();
session.LoadSettings(new AppSettings
{
    EnableVJoy = false,
    DisableKeyboardActions = true,
}, initializeVJoy: false);

ProcessedBalance? latest = null;
session.Processed += data => latest = data;

var result = await session.ConnectWithIntentAsync(ConnectionIntent.QuickReconnect);
await Task.Delay(TimeSpan.FromSeconds(5));

if (latest is not null)
{
    Console.WriteLine($"Weight: {latest.WeightKg:0.0} kg");
    Console.WriteLine($"Lean: X={latest.BalanceX:0.0}% Y={latest.BalanceY:0.0}%");
}

session.Disconnect();
```

## Simulated board (no hardware)

```csharp
using var session = new BalanceBoardSession(connection: new SimulatedBalanceBoardConnection());
await session.ConnectWithIntentAsync(ConnectionIntent.QuickReconnect);
```

## Pure math helpers

| Type | Use |
|------|-----|
| `BalanceMath` | Deadzone, triggers, joy axes, `RestoreAbsoluteWeightKg` |
| `MovementMapper` | Lean flags → slots; `SlotIntensity`, `ScaleMouseAmount` |
| `ActionEngine` | Keyboard/mouse action state machine |

## Further reading

- [ARCHITECTURE.md](ARCHITECTURE.md)
- [CODEMAP.md](CODEMAP.md)
