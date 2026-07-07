using BalanceBoard.Core.Models;
using WiimoteLib;

namespace BalanceBoard.Core.Services;

/// <summary>
/// Ensures WiimoteLib HID probes are torn down before handles go out of scope.
/// Prevents thread-pool OnReadData crashes after Disconnect.
/// </summary>
internal static class WiimoteCollectionHelper
{
    public static IReadOnlyList<string> DiscoverDeviceIds()
    {
        WiimoteCollection? collection = null;
        try
        {
            collection = new WiimoteCollection();
            collection.FindAllWiimotes();
            return EnumerateDeviceIds(collection);
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

    public static int WakeDevices(Action<string>? log)
    {
        WiimoteCollection? collection = null;
        try
        {
            collection = new WiimoteCollection();
            collection.FindAllWiimotes();
            if (collection.Count == 0)
            {
                log?.Invoke("[CONNECT] wake probe: no Wii HID devices visible.");
                return 0;
            }

            log?.Invoke($"[CONNECT] wake probe: {collection.Count} Wii HID device(s) found.");
            var woke = 0;

            foreach (var wii in collection)
            {
                try
                {
                    BalanceBoardProtocol.WakeDeviceSession(wii, log);
                    woke++;
                }
                catch (Exception ex)
                {
                    log?.Invoke($"[CONNECT] HID wake device note: {ex.Message}");
                }
            }

            return woke;
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
