using BalanceBoard.Core.Models;

namespace BalanceBoard.Core.Abstractions;

public interface IBalanceBoardConnection : IDisposable
{
    event Action<string>? StatusChanged;
    event Action<string>? Error;
    event Action<string>? ConnectLog;
    /// <summary>Fired when WiimoteLib reports new sensor data (balance board weight/lean).</summary>
    event Action? ReadingAvailable;

    bool IsConnected { get; }
    string? ConnectedDeviceId { get; }

    IReadOnlyList<string> DiscoverDeviceIds();

    bool Connect(int deviceIndex = 0);

    void Disconnect();

    void Tare();

    BalanceReading? GetCurrentReading();
}
