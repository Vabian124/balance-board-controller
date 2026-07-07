using BalanceBoard.Core.Abstractions;
using BalanceBoard.Core.Models;

namespace BalanceBoard.Core.Services;

/// <summary>
/// Software-only balance board for --simulate-board and headless automation.
/// </summary>
public sealed class SimulatedBalanceBoardConnection : IBalanceBoardConnection
{
    private readonly object _sync = new();
    private DateTime _start = DateTime.UtcNow;

    public event Action<string>? StatusChanged;
#pragma warning disable CS0067
    public event Action<string>? Error;
#pragma warning restore CS0067
    public event Action<string>? ConnectLog;

    public bool IsConnected { get; private set; }
    public string? ConnectedDeviceId { get; private set; }

    public IReadOnlyList<string> DiscoverDeviceIds() => ["SIM-BOARD-001"];

    public bool Connect(int deviceIndex = 0)
    {
        Disconnect();
        ConnectedDeviceId = DiscoverDeviceIds()[0];
        IsConnected = true;
        _start = DateTime.UtcNow;
        ConnectLog?.Invoke($"[CONNECT] Simulated HID connect to {ConnectedDeviceId}");
        StatusChanged?.Invoke($"Connected to simulated balance board {ConnectedDeviceId}.");
        return true;
    }

    public void Disconnect()
    {
        IsConnected = false;
        ConnectedDeviceId = null;
    }

    public void Tare()
    {
        _start = DateTime.UtcNow;
    }

    public BalanceReading? GetCurrentReading()
    {
        lock (_sync)
        {
            if (!IsConnected)
            {
                return null;
            }

            var t = (DateTime.UtcNow - _start).TotalSeconds;
            var wave = (float)Math.Sin(t * 0.8) * 8f;
            var baseKg = 62f + wave;
            return new BalanceReading
            {
                WeightKg = baseKg,
                TopLeftKg = baseKg * 0.25f + wave * 0.1f,
                TopRightKg = baseKg * 0.25f - wave * 0.1f,
                BottomLeftKg = baseKg * 0.25f - wave * 0.05f,
                BottomRightKg = baseKg * 0.25f + wave * 0.05f,
                IsBalanceBoard = true,
            };
        }
    }

    public void Dispose() => Disconnect();
}
