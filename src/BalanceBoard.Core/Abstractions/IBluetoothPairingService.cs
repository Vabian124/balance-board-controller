using BalanceBoard.Core.Services.Connection;

namespace BalanceBoard.Core.Abstractions;

public interface IBluetoothPairingService
{
    bool IsBluetoothAvailable();

    /// <summary>Returns the primary adapter MAC (12 hex digits, no separators) or null.</summary>
    string? TryGetLocalAdapterMac();

    void WakePairedDevices(Action<string>? log = null);

    BluetoothPairingResult PairDiscoverableBoard(
        Action<string>? log = null,
        CancellationToken cancellationToken = default,
        bool removeStalePairings = true);
}
