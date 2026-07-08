namespace BalanceBoard.Core.Services.Session;

/// <summary>
/// QuickReconnect: HID connect only (already paired board). PairAndConnect: full Bluetooth pairing.
/// </summary>
public enum ConnectionIntent
{
    QuickReconnect,
    PairAndConnect,
}
