using BalanceBoard.Core.Models;
using BalanceBoard.Core.Services.Diagnostics;
using WiimoteLib;

namespace BalanceBoard.Core.Services.Connection;

/// <summary>
/// Ensures WiimoteLib HID probes are torn down before handles go out of scope.
/// Prevents thread-pool OnReadData crashes after Disconnect.
/// </summary>
internal static class WiimoteCollectionHelper
{
    /// <summary>Serializes all WiimoteLib HID open/close so wake probes cannot race Connect.</summary>
    internal static readonly object HidGate = new();

    public static IReadOnlyList<string> DiscoverDeviceIds(Action<string>? log = null) =>
        DiscoverDeviceIdsCore(log);

    private static IReadOnlyList<string> DiscoverDeviceIdsCore(Action<string>? log)
    {
        if (!Monitor.TryEnter(HidGate, TimeSpan.FromSeconds(BalanceConstants.HidDiscoveryTimeoutSeconds)))
        {
            log?.Invoke("[CONNECT] HID discovery skipped — device layer busy (try again shortly).");
            // #region agent log
            AgentDebugLog.Write("H11", "WiimoteCollectionHelper.DiscoverDeviceIds", "hid gate busy");
            // #endregion
            return Array.Empty<string>();
        }

        try
        {
            WiimoteCollection? collection = null;
            try
            {
                collection = new WiimoteCollection();
                collection.FindAllWiimotes();
                var ids = EnumerateDeviceIds(collection);
                if (log is not null && ids.Count > 0)
                {
                    log.Invoke($"[CONNECT] HID enumeration: {ids.Count} device(s): {string.Join(", ", ids)}");
                }

                return ids;
            }
            catch (WiimoteNotFoundException)
            {
                return Array.Empty<string>();
            }
            finally
            {
                ReleaseAll(collection);
            }
        }
        finally
        {
            Monitor.Exit(HidGate);
        }
    }

    public static int WakeDevices(Action<string>? log)
    {
        if (!Monitor.TryEnter(HidGate, TimeSpan.FromSeconds(BalanceConstants.HidDiscoveryTimeoutSeconds)))
        {
            log?.Invoke("[CONNECT] wake probe: HID layer busy — skipping wake ping.");
            return 0;
        }

        try
        {
            return WakeDevicesCore(log);
        }
        finally
        {
            Monitor.Exit(HidGate);
        }
    }

    private static int WakeDevicesCore(Action<string>? log)
    {
        WiimoteCollection? collection = null;
        var wokeCount = 0;
        try
        {
            collection = new WiimoteCollection();
            try
            {
                collection.FindAllWiimotes();
            }
            catch (WiimoteNotFoundException)
            {
                log?.Invoke("[CONNECT] wake probe: no Wii HID devices visible.");
                return 0;
            }

            if (collection.Count == 0)
            {
                log?.Invoke("[CONNECT] wake probe: no Wii HID devices visible.");
                return 0;
            }

            log?.Invoke($"[CONNECT] wake probe: {collection.Count} Wii HID device(s) found.");

            foreach (var wii in collection)
            {
                try
                {
                    var path = DeviceIdRules.ExtractFromHidPath(wii.HIDDevicePath);
                    log?.Invoke($"[CONNECT] wake probe: HID ping id={path ?? "?"} path={wii.HIDDevicePath}");
                    BalanceBoardProtocol.WakeDeviceSession(wii, log);
                    wokeCount++;
                }
                catch (Exception ex)
                {
                    log?.Invoke($"[CONNECT] HID wake device note: {ex.Message}");
                    // #region agent log
                    AgentDebugLog.Write(
                        "H12",
                        "WiimoteCollectionHelper.WakeDevices",
                        "wake ping error",
                        new { error = ex.Message });
                    // #endregion
                }
            }

            return wokeCount;
        }
        catch (Exception ex)
        {
            log?.Invoke($"[CONNECT] HID wake-up error: {ex.Message}");
            if (!string.IsNullOrWhiteSpace(ex.StackTrace))
            {
                log?.Invoke(ex.StackTrace);
            }

            return 0;
        }
        finally
        {
            ReleaseAll(collection, extendedDrain: true);
        }
    }

    public static void ReleaseAll(WiimoteCollection? collection, bool extendedDrain = false)
    {
        if (collection is null)
        {
            return;
        }

        foreach (var wii in collection)
        {
            SafeDisconnect(wii);
        }

        Thread.Sleep(
            extendedDrain
                ? BalanceConstants.HidCallbackDrainMs + BalanceConstants.WakeProbePostDisconnectDrainMs
                : BalanceConstants.HidCallbackDrainMs);
    }

    public static void SafeDisconnect(Wiimote? wii)
    {
        if (wii is null)
        {
            return;
        }

        try
        {
            wii.Disconnect();
        }
        catch
        {
            // Best-effort — handle may already be closed.
        }

        Thread.Sleep(BalanceConstants.DisconnectGraceMs);
    }

    private static IReadOnlyList<string> EnumerateDeviceIds(WiimoteCollection collection) =>
        collection
            .Select(d => DeviceIdRules.ExtractFromHidPath(d.HIDDevicePath))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .ToList();
}
