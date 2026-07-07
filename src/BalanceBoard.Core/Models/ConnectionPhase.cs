namespace BalanceBoard.Core.Models;

/// <summary>
/// Session-level connection state for UI. Distinct from Windows Bluetooth pairing.
/// </summary>
public enum ConnectionPhase
{
    Offline,
    Connecting,
    Connected,
    Reconnecting,
    /// <summary>Bluetooth radio on and board paired in Windows, but HID session not alive yet.</summary>
    PairedReconnecting,
}
