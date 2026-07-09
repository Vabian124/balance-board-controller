using BalanceBoard.Core.Services.Diagnostics;
using InTheHand.Net;
using InTheHand.Net.Bluetooth;
using InTheHand.Net.Sockets;

namespace BalanceBoard.Core.Services.Connection;

/// <summary>Structured Bluetooth/HID snapshot logging for connect debugging.</summary>
internal static class BluetoothDiagnostics
{
    public static void LogSnapshot(Action<string>? log, string phase, bool includeHidProbe = true)
    {
        if (log is null)
        {
            return;
        }

        try
        {
            var radio = BluetoothRadio.PrimaryRadio;
            if (radio is null)
            {
                log.Invoke($"[BT] {phase}: no primary radio");
                return;
            }

            log.Invoke(
                $"[BT] {phase}: radio={radio.Name} mode={radio.Mode} " +
                $"local={WiiBluetoothPin.FormatMacForDisplay(radio.LocalAddress.ToString().Replace(":", ""))}");

            using var client = new BluetoothClient();
            client.InquiryLength = TimeSpan.FromSeconds(1);

            var remembered = client.DiscoverDevices(255, false, true, false);
            var nintendoRemembered = remembered.Where(IsNintendoDevice).ToList();
            log.Invoke($"[BT] {phase}: remembered={remembered.Length} nintendo={nintendoRemembered.Count}");
            foreach (var device in nintendoRemembered)
            {
                log.Invoke(
                    $"[BT]   {device.DeviceName} addr={device.DeviceAddress} " +
                    $"connected={device.Connected} authenticated={device.Authenticated}");
            }

            IReadOnlyList<string> hidIds = Array.Empty<string>();
            if (includeHidProbe)
            {
                hidIds = WiimoteCollectionHelper.DiscoverDeviceIds(log);
                log.Invoke(
                    hidIds.Count > 0
                        ? $"[BT] {phase}: wiiHid=[{string.Join(", ", hidIds)}]"
                        : $"[BT] {phase}: wiiHid=none");
            }
            else
            {
                log.Invoke($"[BT] {phase}: wiiHid=skipped (avoid nested HID probe during wake)");
            }

            // #region agent log
            AgentDebugLog.Write(
                "H11",
                "BluetoothDiagnostics.LogSnapshot",
                phase,
                new
                {
                    radioMode = radio.Mode.ToString(),
                    remembered = remembered.Length,
                    nintendoRemembered = nintendoRemembered.Count,
                    hidCount = hidIds.Count,
                    hidIds,
                    includeHidProbe,
                });
            // #endregion
        }
        catch (Exception ex)
        {
            log.Invoke($"[BT] {phase}: snapshot error: {ex.Message}");
        }
    }

    public static void LogDiscoverableInquiry(Action<string>? log, BluetoothDeviceInfo[] devices)
    {
        if (log is null)
        {
            return;
        }

        log.Invoke($"[BT] inquiry: discoverable={devices.Length}");
        foreach (var device in devices)
        {
            var tag = IsNintendoDevice(device) ? "Nintendo" : "other";
            log.Invoke(
                $"[BT]   [{tag}] {device.DeviceName} addr={device.DeviceAddress} " +
                $"connected={device.Connected} authenticated={device.Authenticated}");
        }

        // #region agent log
        AgentDebugLog.Write(
            "H11",
            "BluetoothDiagnostics.LogDiscoverableInquiry",
            "inquiry results",
            new
            {
                total = devices.Length,
                nintendo = devices.Count(IsNintendoDevice),
            });
        // #endregion
    }

    private static bool IsNintendoDevice(BluetoothDeviceInfo device) =>
        device.DeviceName.Contains("Nintendo", StringComparison.OrdinalIgnoreCase);
}
