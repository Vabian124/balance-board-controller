namespace BalanceBoard.Testing;

/// <summary>
/// WiiBalanceWalker connect scenarios derived from FormMain (Connect) and FormBluetooth (pair/wake).
/// Used by <see cref="ScriptedBluetoothPairingService"/> and integration tests.
/// </summary>
public enum ReferenceConnectScenario
{
    /// <summary>FormMain: FindAllWiimotes finds HID — Connect immediately, no Bluetooth wake.</summary>
    FormMainHidVisible,

    /// <summary>FormBluetooth: no HID → inquiry finds Nintendo (SYNC) → pair → 4s → wake ping → HID.</summary>
    FormBluetoothFreshSyncPair,

    /// <summary>Windows remembers Nintendo but board not discoverable (inquiry finds phone only).</summary>
    StaleWindowsPairingNoSync,

    /// <summary>User removed Windows pairing; inquiry finds Nintendo on later wake attempt.</summary>
    DeletedPairingThenSync,

    /// <summary>Windows remembers board; user standing on it — BT reconnect restores HID without SYNC.</summary>
    PreparedBoardOnFeet,

    /// <summary>Board off / not discoverable — inquiry finds nothing.</summary>
    BoardOffNoDiscoverable,
}
