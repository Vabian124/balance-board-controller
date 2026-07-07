using System.Text.RegularExpressions;

namespace BalanceBoard.Core.Models;

/// <summary>
/// Helpers for persisted board IDs — excludes software-only simulate/automation devices.
/// </summary>
public static class DeviceIdRules
{
    public const string SimulatedDeviceId = "SIM-BOARD-001";

    /// <summary>WiiBalanceWalker: instance id is the segment after the product id (e.g. 0306 → 37A15347).</summary>
    private static readonly Regex HidInstanceIdRegex = new(
        "e_pid&[^&#]+&([^&#]+)&",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Windows HID path: …_pid&amp;0306#9&amp;37a15347&amp;… (no e_pid prefix).</summary>
    private static readonly Regex HidWindowsInstanceIdRegex = new(
        "_pid&[^&#]+(?:#[^&]*)?&([^&#]+)&",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex HidDeviceIdRegex = new(
        "e_pid&([^&#]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool IsSimulated(string? deviceId) =>
        !string.IsNullOrWhiteSpace(deviceId)
        && deviceId.StartsWith("SIM-", StringComparison.OrdinalIgnoreCase);

    public static bool ShouldPersistConnectionState(string? deviceId) =>
        !string.IsNullOrWhiteSpace(deviceId) && !IsSimulated(deviceId);

    public static string? ExtractFromHidPath(string hidPath)
    {
        if (string.IsNullOrWhiteSpace(hidPath))
        {
            return null;
        }

        var instanceMatch = HidInstanceIdRegex.Match(hidPath);
        if (instanceMatch.Success)
        {
            var instanceId = instanceMatch.Groups[1].Value;
            if (IsValidInstanceId(instanceId))
            {
                return instanceId.ToUpperInvariant();
            }
        }

        var windowsMatch = HidWindowsInstanceIdRegex.Match(hidPath);
        if (windowsMatch.Success)
        {
            var instanceId = windowsMatch.Groups[1].Value;
            if (IsValidInstanceId(instanceId))
            {
                return instanceId.ToUpperInvariant();
            }
        }

        var match = HidDeviceIdRegex.Match(hidPath);
        return match.Success ? match.Groups[1].Value.ToUpperInvariant() : hidPath;
    }

    private static bool IsValidInstanceId(string instanceId) =>
        instanceId.Length >= 4 && !string.Equals(instanceId, "0", StringComparison.Ordinal);
}
