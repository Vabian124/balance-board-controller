using BalanceBoard.Core.Abstractions;
using BalanceBoard.Core.Services;

namespace BalanceBoard.Testing;

public sealed class FakeBluetoothPairingService : IBluetoothPairingService
{
    private readonly Queue<BluetoothPairingResult> _pairResults = new();

    public int WakeCallCount { get; private set; }
    public int PairCallCount { get; private set; }
    public bool BluetoothAvailable { get; set; } = true;

    public bool IsBluetoothAvailable() => BluetoothAvailable;

    public void EnqueuePairResult(BluetoothPairingResult result) => _pairResults.Enqueue(result);

    public void WakePairedDevices(Action<string>? log = null)
    {
        WakeCallCount++;
        log?.Invoke("Fake wake paired devices.");
    }

    public BluetoothPairingResult PairDiscoverableBoard(
        Action<string>? log = null,
        CancellationToken cancellationToken = default,
        bool removeStalePairings = true)
    {
        PairCallCount++;
        cancellationToken.ThrowIfCancellationRequested();

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
