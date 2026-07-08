namespace BalanceBoard.Core.Services.Connection;

/// <summary>
/// Builds the Wii permanent-sync PIN from the host Bluetooth MAC (reversed byte pairs).
/// Ported from WiiBalanceWalker FormBluetooth.AddressToWiiPin.
/// </summary>
public static class WiiBluetoothPin
{
    public static bool TryCreateFromHostMac(string bluetoothAddress, out string pin, out string? error)
    {
        pin = string.Empty;
        error = null;

        var normalized = bluetoothAddress.Replace(":", "").Replace("-", "").Trim();
        if (normalized.Length != 12)
        {
            error = $"Invalid Bluetooth adapter address: {bluetoothAddress}";
            return false;
        }

        var pinBuilder = string.Empty;
        var hasDoubleZero = false;
        for (var i = normalized.Length - 2; i >= 0; i -= 2)
        {
            var hex = normalized.Substring(i, 2);
            pinBuilder += (char)Convert.ToInt32(hex, 16);
            if (hex.Equals("00", StringComparison.OrdinalIgnoreCase))
            {
                hasDoubleZero = true;
            }
        }

        if (hasDoubleZero)
        {
            error =
                "This PC's Bluetooth MAC contains 00, so Wii permanent pairing may not work. " +
                "Try a USB Bluetooth adapter with a different MAC, or use WiiBalanceWalker's workaround tools.";
            return false;
        }

        pin = pinBuilder;
        return true;
    }

    public static string FormatMacForDisplay(string bluetoothAddress)
    {
        var normalized = bluetoothAddress.Replace(":", "").Replace("-", "").Trim();
        if (normalized.Length != 12)
        {
            return bluetoothAddress;
        }

        return string.Join(":", Enumerable.Range(0, 6).Select(i => normalized.Substring(i * 2, 2)));
    }
}
