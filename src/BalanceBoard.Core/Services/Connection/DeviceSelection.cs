namespace BalanceBoard.Core.Services;

/// <summary>Helpers for choosing among multiple discovered balance-board HID device ids.</summary>
public static class DeviceSelection
{
    /// <summary>
    /// Returns the index of <paramref name="preferredDeviceId"/> in <paramref name="deviceIds"/>,
    /// or -1 when not present / not specified.
    /// </summary>
    public static int IndexOfPreferred(IReadOnlyList<string> deviceIds, string? preferredDeviceId)
    {
        if (deviceIds.Count == 0 || string.IsNullOrWhiteSpace(preferredDeviceId))
        {
            return -1;
        }

        for (var i = 0; i < deviceIds.Count; i++)
        {
            if (string.Equals(deviceIds[i], preferredDeviceId, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Resolve which device index to connect: preferred if present, else 0.
    /// When <paramref name="allowAmbiguousDefault"/> is false and multiple devices have no preferred match,
    /// returns null so the UI can present a picker.
    /// </summary>
    public static int? ResolveDeviceIndex(
        IReadOnlyList<string> deviceIds,
        string? preferredDeviceId,
        bool allowAmbiguousDefault)
    {
        if (deviceIds.Count <= 1)
        {
            return 0;
        }

        var preferred = IndexOfPreferred(deviceIds, preferredDeviceId);
        if (preferred >= 0)
        {
            return preferred;
        }

        return allowAmbiguousDefault ? 0 : null;
    }
}
