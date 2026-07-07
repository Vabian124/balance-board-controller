using System.Text.RegularExpressions;

namespace BalanceBoard.Core.Models;

/// <summary>
/// Helpers for persisted board IDs — excludes software-only simulate/automation devices.
/// </summary>
public static class DeviceIdRules
{
    public const string SimulatedDeviceId = "SIM-BOARD-001";

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

        var match = HidDeviceIdRegex.Match(hidPath);
        return match.Success ? match.Groups[1].Value.ToUpperInvariant() : hidPath;
    }
}
