using BalanceBoard.Core.Services.Connection;

namespace BalanceBoard.Core.Abstractions;

public interface IBluetoothPairingService
{
    bool IsBluetoothAvailable();

    /// <summary>Returns the primary adapter MAC (12 hex digits, no separators) or null.</summary>
    string? TryGetLocalAdapterMac();

    /// <summary>True when Windows still has a remembered Nintendo/Wii pairing entry.</summary>
    bool HasRememberedNintendoDevices(Action<string>? log = null);

    /// <summary>WiiBalanceWalker v1.4 wake: enable HID, inquiry if needed, Wiimote wake ping.</summary>
    void WakePairedDevices(Action<string>? log = null, CancellationToken cancellationToken = default);

    BluetoothPairingResult PairDiscoverableBoard(
        Action<string>? log = null,
        CancellationToken cancellationToken = default,
        bool removeStalePairings = true);
}
