namespace BalanceBoard.Testing;

/// <summary>Ordered markers from reference/WiiBalanceWalker FormMain + FormBluetooth.</summary>
public static class WiiBalanceWalkerReferenceSteps
{
    public static readonly IReadOnlyList<string> FormMainConnect =
    [
        "FindAllWiimotes",
        "Connect",
        "SetReportType",
        "SetLEDs",
    ];

    public static readonly IReadOnlyList<string> FormBluetoothPairWake =
    [
        "RemoveExistingNintendoDevices", // optional when removeStalePairings
        "DiscoverDevices discoverable",
        "PairRequest",
        "SetServiceState HumanInterfaceDevice",
        "Thread.Sleep 4000",
        "FindAllWiimotes",
        "Connect",
        "SetLEDs",
        "Disconnect",
    ];

    public static readonly IReadOnlyList<string> BalanceBoardAppWakeV14 =
    [
        "wake probe: starting paired-device wake sequence",
        "enabled HID",
        "[BT] reconnect:",
        "Bluetooth inquiry for discoverable board",
        "Post-pair: waiting for Windows HID enumeration",
        "wake probe: brief wake",
    ];

    public static bool ContainsInOrder(IReadOnlyList<string> logLines, IReadOnlyList<string> markers)
    {
        var joined = string.Join('\n', logLines);
        var index = 0;
        foreach (var marker in markers)
        {
            var found = joined.IndexOf(marker, index, StringComparison.OrdinalIgnoreCase);
            if (found < 0)
            {
                return false;
            }

            index = found + marker.Length;
        }

        return true;
    }
}
