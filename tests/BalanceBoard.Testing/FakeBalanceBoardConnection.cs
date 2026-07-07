using BalanceBoard.Core.Abstractions;
using BalanceBoard.Core.Models;

namespace BalanceBoard.Testing;

public sealed class FakeBalanceBoardConnection : IBalanceBoardConnection
{
    private readonly object _sync = new();

    public Func<int, bool>? ConnectHandler { get; set; }
    public IReadOnlyList<string> DiscoveredDevices { get; set; } = ["FAKE-BOARD-001"];
    public bool ReturnNotBalanceBoard { get; set; }
    public bool FireReadingAfterDisconnect { get; set; }
    public Exception? ConnectException { get; set; }
    public int ConnectAttempts { get; private set; }
    public int DisconnectCount { get; private set; }

    public event Action<string>? StatusChanged;
#pragma warning disable CS0067
    public event Action<string>? Error;
#pragma warning restore CS0067
    public event Action? ReadingAvailable;
    public event Action<string>? ConnectLog;

    public bool IsConnected { get; private set; }
    public string? ConnectedDeviceId { get; private set; }

    public IReadOnlyList<string> DiscoverDeviceIds() => DiscoveredDevices;

    public bool Connect(int deviceIndex = 0)
    {
        ConnectAttempts++;
        if (ConnectException is not null)
        {
            throw ConnectException;
        }

        if (ConnectHandler is not null)
        {
            return ConnectHandler(deviceIndex);
        }

        if (DiscoveredDevices.Count == 0)
        {
            StatusChanged?.Invoke("No devices.");
            return false;
        }

        ConnectedDeviceId = DiscoveredDevices[Math.Min(deviceIndex, DiscoveredDevices.Count - 1)];
        IsConnected = true;
        ConnectLog?.Invoke($"[CONNECT] Fake HID connect to {ConnectedDeviceId}");
        if (ReturnNotBalanceBoard)
        {
            StatusChanged?.Invoke("Not a balance board.");
            IsConnected = false;
            return false;
        }

        StatusChanged?.Invoke($"Connected to {ConnectedDeviceId}.");
        return true;
    }

    public void Disconnect()
    {
        DisconnectCount++;
        IsConnected = false;
        ConnectedDeviceId = null;
        if (FireReadingAfterDisconnect)
        {
            ReadingAvailable?.Invoke();
        }
    }

    public void Tare()
    {
    }

    public BalanceReading? GetCurrentReading()
    {
        lock (_sync)
        {
            if (!IsConnected)
            {
                return null;
            }

            return new BalanceReading
            {
                WeightKg = 60f,
                TopLeftKg = 15f,
                TopRightKg = 15f,
                BottomLeftKg = 15f,
                BottomRightKg = 15f,
                IsBalanceBoard = !ReturnNotBalanceBoard,
            };
        }
    }

    public void Dispose() => Disconnect();
}
