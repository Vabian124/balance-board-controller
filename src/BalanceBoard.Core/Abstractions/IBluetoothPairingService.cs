using BalanceBoard.Core.Services;

namespace BalanceBoard.Core.Abstractions;

public interface IBluetoothPairingService
{
    void WakePairedDevices(Action<string>? log = null);

    BluetoothPairingResult PairDiscoverableBoard(
        Action<string>? log = null,
        CancellationToken cancellationToken = default,
        bool removeStalePairings = true);
}
