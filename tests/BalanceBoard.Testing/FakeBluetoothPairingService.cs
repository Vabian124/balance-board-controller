using BalanceBoard.Core.Abstractions;
using BalanceBoard.Core.Services;

namespace BalanceBoard.Testing;

public sealed class FakeBluetoothPairingService : IBluetoothPairingService
{
    private readonly Queue<BluetoothPairingResult> _pairResults = new();

    public int WakeCallCount { get; private set; }
    public int PairCallCount { get; private set; }
    public bool BluetoothAvailable { get; set; } = true;
    public string? AdapterMac { get; set; } = "001A7DDA7113";
    public int PairDelayMs { get; set; }

    public bool IsBluetoothAvailable() => BluetoothAvailable;

    public string? TryGetLocalAdapterMac() => AdapterMac;

    public void EnqueuePairResult(BluetoothPairingResult result) => _pairResults.Enqueue(result);

    public void WakePairedDevices(Action<string>? log = null)
    {
        WakeCallCount++;
        log?.Invoke("[CONNECT] wake probe: starting paired-device wake sequence (fake).");
    }

    public BluetoothPairingResult PairDiscoverableBoard(
        Action<string>? log = null,
        CancellationToken cancellationToken = default,
        bool removeStalePairings = true)
    {
        PairCallCount++;
        cancellationToken.ThrowIfCancellationRequested();

        if (PairDelayMs > 0)
        {
            if (cancellationToken.WaitHandle.WaitOne(PairDelayMs))
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        if (_pairResults.Count > 0)
        {
            var result = _pairResults.Dequeue();
            log?.Invoke(result.Message);
            return result;
        }

        return new BluetoothPairingResult
        {
            Success = false,
            Message = "No fake pairing result queued.",
        };
    }
}
