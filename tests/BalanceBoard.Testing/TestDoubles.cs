using BalanceBoard.Core.Abstractions;
using BalanceBoard.Core.Models;

namespace BalanceBoard.Testing;

public sealed class NullGameControllerOutput : IGameControllerOutput
{
    public bool IsReady => false;

    public bool Initialize(uint deviceId = 1, bool attemptCleanupOnBusy = true) => false;

    public void Update(ProcessedBalance data)
    {
    }

    public void Center()
    {
    }

    public void Shutdown()
    {
    }

    public void Dispose()
    {
    }
}

public sealed class NullActionSimulator : IActionSimulator
{
    public void Apply(ProcessedBalance data, AppSettings settings)
    {
    }

    public void ReleaseAll()
    {
    }
}
