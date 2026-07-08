using BalanceBoard.Core.Models;
using WiimoteLib;

namespace BalanceBoard.Core.Services;

/// <summary>
/// Wii Balance Board HID constants and handshake helpers (WiiBrew / WiimoteLib).
/// PC hosts must write <c>0x55</c> to extension register <c>0xA400F0</c> — never Wii-style <c>0xAA</c> encryption init.
/// </summary>
public static class BalanceBoardProtocol
{
    /// <summary>Encrypted extension type half-word at register <c>0xA400FA</c> (bytes 4–5 of the 6-byte ID read).</summary>
    public const ushort ExtensionEncryptedId = 0x0402;

    /// <summary>Decrypted extension type for the balance board (WiiBrew).</summary>
    public const ushort ExtensionDecryptedId = 0x2A2C;

    /// <summary>Input report: core buttons + 19 extension bytes (weight + battery).</summary>
    public const byte WeightReportId = 0x34;

    /// <summary>Output report: set data reporting mode — continuous <c>0x12 0x04 0x34</c>.</summary>
    public const string ContinuousReportModeHex = "12 04 34";

    /// <summary>
    /// Force continuous <see cref="InputReport.ButtonsExtension"/> (0x34) streaming.
    /// WiimoteLib maps <see cref="InputReport.IRAccel"/> to the same mode for balance boards.
    /// </summary>
    public static void ApplyContinuousWeightReports(Wiimote device, Action<string>? log = null)
    {
        device.SetReportType(InputReport.ButtonsExtension, true);
        log?.Invoke($"[CONNECT] Report mode {ContinuousReportModeHex} (continuous ButtonsExtension / 0x{WeightReportId:X2})");
    }

    /// <summary>Short HID wake: connect + LED only (WiiBalanceWalker FormBluetooth — no continuous 0x34 reports).</summary>
    public static void WakeDeviceSession(Wiimote device, Action<string>? log = null)
    {
        log?.Invoke("[CONNECT] wake probe: brief HID wake (LED only).");
        device.Connect();
        device.SetLEDs(true, false, false, false);
        Thread.Sleep(BalanceConstants.WakeProbeMinimalHoldMs);
    }

    public static string FormatExtensionDiagnostic(ExtensionType type)
    {
        if (type == ExtensionType.None)
        {
            return "extension=none";
        }

        var encrypted = (ushort)((ulong)(long)type & 0xFFFF);
        return type == ExtensionType.BalanceBoard
            ? $"extension id=0x{encrypted:X4} (Balance Board, decrypted 0x{ExtensionDecryptedId:X4})"
            : $"extension id=0x{encrypted:X4} ({type})";
    }
}
