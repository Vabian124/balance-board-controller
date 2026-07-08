using BalanceBoard.Core.Models;

namespace BalanceBoard.Core.Abstractions;

/// <summary>
/// Platform adapter for virtual game-controller output. Implement with vJoy (.NET) or pyvjoy (Python).
/// </summary>
public interface IGameControllerOutput : IDisposable
{
    bool IsReady { get; }

    bool Initialize(uint deviceId = 1, bool attemptCleanupOnBusy = true);

    void Update(ProcessedBalance data, AppSettings settings);

    void Center();

    void Shutdown();
}
