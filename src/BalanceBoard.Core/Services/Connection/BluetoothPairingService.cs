using System.Reflection;
using BalanceBoard.Core.Abstractions;
using BalanceBoard.Core.Models;
using BalanceBoard.Core.Services.Diagnostics;
using InTheHand.Net;
using InTheHand.Net.Bluetooth;
using InTheHand.Net.Sockets;

namespace BalanceBoard.Core.Services.Connection;

public sealed class BluetoothPairingResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public int DevicesPaired { get; init; }
}

/// <summary>
/// Automatic Wii Balance Board Bluetooth pairing using the host MAC reversed PIN (WiiBalanceWalker method).
/// </summary>
public sealed class BluetoothPairingService : IBluetoothPairingService
{
    public bool IsBluetoothAvailable()
    {
        var mac = TryGetLocalAdapterMac();
        if (!string.IsNullOrWhiteSpace(mac))
        {
            return true;
        }

        return ProbeRadio(out _) && GetRadioMode() != RadioMode.PowerOff;
    }

    /// <summary>WiimoteLib HID probes can briefly break InTheHand — retry with warmup.</summary>
    public bool EnsureBluetoothReady(Action<string>? log = null)
    {
        var mac = TryGetLocalAdapterMac();
        if (!string.IsNullOrWhiteSpace(mac))
        {
            if (ProbeRadio(out var modeName) && GetRadioMode() == RadioMode.PowerOff)
            {
                log?.Invoke(
                    $"[CONNECT] Adapter {WiiBluetoothPin.FormatMacForDisplay(mac)} readable — " +
                    $"continuing despite stale radio mode ({modeName}).");
            }

            return true;
        }

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            if (ProbeRadio(out var modeName))
            {
                var mode = GetRadioMode();
                if (mode is not null and not RadioMode.PowerOff)
                {
                    if (attempt > 1)
                    {
                        log?.Invoke($"[CONNECT] Bluetooth radio ready after recovery (mode={mode}, attempt={attempt}).");
                    }

                    return true;
                }
            }

            if (attempt < 3)
            {
                Warmup();
                Thread.Sleep(250);
            }
        }

        return false;
    }

    private static RadioMode? GetRadioMode()
    {
        try
        {
            return BluetoothRadio.PrimaryRadio?.Mode;
        }
        catch
        {
            return null;
        }
    }

    private static bool ProbeRadio(out string? modeName)
    {
        modeName = null;
        try
        {
            var radio = BluetoothRadio.PrimaryRadio;
            if (radio is null)
            {
                return false;
            }

            modeName = radio.Mode.ToString();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public string? TryGetLocalAdapterMac()
    {
        try
        {
            var radio = BluetoothRadio.PrimaryRadio;
            if (radio is null)
            {
                return null;
            }

            return radio.LocalAddress.ToString().Replace(":", "");
        }
        catch
        {
            return null;
        }
    }

    public static void Warmup()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "InTheHand.Net.Personal.dll");
            if (File.Exists(path))
            {
                Assembly.LoadFrom(path);
            }

            using var client = new BluetoothClient();
            _ = BluetoothRadio.PrimaryRadio;
        }
        catch
        {
            // Warmup is best-effort; pairing will report a clear error if Bluetooth is unavailable.
        }
    }

    public BluetoothPairingResult PairDiscoverableBoard(
        Action<string>? log = null,
        CancellationToken cancellationToken = default,
        bool removeStalePairings = true)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsBluetoothAvailable())
            {
                return Fail("Bluetooth is turned off — turn it on in Windows settings, then try Connect again.");
            }

            using var btClient = new BluetoothClient();
            btClient.InquiryLength = TimeSpan.FromSeconds(BalanceConstants.BluetoothInquirySeconds);
            var radio = BluetoothRadio.PrimaryRadio;
            if (radio is null)
            {
                return Fail("No Bluetooth radio found on this PC.");
            }

            var hostMac = TryGetLocalAdapterMac();
            if (hostMac is null)
            {
                return Fail("No Bluetooth radio found on this PC.");
            }

            if (!WiiBluetoothPin.TryCreateFromHostMac(hostMac, out var pin, out var pinError))
            {
                return Fail(pinError ?? "Could not build Wii pairing PIN from this adapter.");
            }

            log?.Invoke($"Bluetooth adapter {WiiBluetoothPin.FormatMacForDisplay(hostMac)} — using automatic permanent Wii PIN.");

            if (removeStalePairings)
            {
                log?.Invoke("Removing stale Nintendo pairings…");
                RemoveExistingNintendoDevices(btClient);
            }

            cancellationToken.ThrowIfCancellationRequested();

            log?.Invoke("Searching for balance board — press the red SYNC button under the battery cover.");
            var discovered = btClient.DiscoverDevices(255, false, false, true);
            BluetoothDiagnostics.LogDiscoverableInquiry(log, discovered);
            var paired = 0;

            foreach (var device in discovered)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!IsNintendoDevice(device))
                {
                    continue;
                }

                log?.Invoke($"Pairing {device.DeviceName} ({device.DeviceAddress})…");

                _ = new BluetoothWin32Authentication(device.DeviceAddress, pin);
                BluetoothSecurity.PairRequest(device.DeviceAddress, null);
                device.SetServiceState(BluetoothService.HumanInterfaceDevice, true);
                paired++;
            }

            if (paired == 0)
            {
                return Fail("No Nintendo device found. Press SYNC on the board and try again.");
            }

            log?.Invoke("Finishing Bluetooth setup…");
            if (cancellationToken.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(BalanceConstants.BluetoothFinishWaitMs)))
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            cancellationToken.ThrowIfCancellationRequested();
            // FormBluetooth: 4s HID install wait + Connect/LED/Disconnect wake ping (inline — callers skip duplicate wake).
            RunFormBluetoothWakePing(log, cancellationToken);
            return new BluetoothPairingResult
            {
                Success = true,
                Message = $"Paired {paired} Nintendo device(s).",
                DevicesPaired = paired,
            };
        }
        catch (OperationCanceledException)
        {
            return Fail("Pairing cancelled.");
        }
        catch (Exception ex)
        {
            log?.Invoke($"[CONNECT] Pairing error: {ex.Message}");
            if (!string.IsNullOrWhiteSpace(ex.StackTrace))
            {
                log?.Invoke(ex.StackTrace);
            }

            return Fail(ex.Message);
        }
    }

    private static void RemoveExistingNintendoDevices(BluetoothClient btClient)
    {
        btClient.InquiryLength = TimeSpan.FromSeconds(BalanceConstants.BluetoothInquirySeconds);
        var existing = btClient.DiscoverDevices(255, false, true, false);
        foreach (var device in existing)
        {
            if (!IsNintendoDevice(device))
            {
                continue;
            }

            BluetoothSecurity.RemoveDevice(device.DeviceAddress);
            device.SetServiceState(BluetoothService.HumanInterfaceDevice, false);
        }
    }

    public bool HasRememberedNintendoDevices(Action<string>? log = null) =>
        GetRememberedNintendoDevices(log).Count > 0;

    public void WakePairedDevices(
        Action<string>? log = null,
        CancellationToken cancellationToken = default)
    {
        log?.Invoke("[CONNECT] wake probe: starting paired-device wake sequence (v1.4 flow).");
        BluetoothDiagnostics.LogSnapshot(log, "wake-start", includeHidProbe: false);

        cancellationToken.ThrowIfCancellationRequested();
        var remembered = EnableHidOnRememberedNintendoDevices(log, cancellationToken);

        if (cancellationToken.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(BalanceConstants.PostPairSettleMs)))
        {
            cancellationToken.ThrowIfCancellationRequested();
        }

        if (WiimoteCollectionHelper.DiscoverDeviceIds(log).Count == 0 && remembered.Count > 0)
        {
            TryReconnectRememberedDevices(remembered, log, cancellationToken);
            if (cancellationToken.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(BalanceConstants.PostPairSettleMs)))
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        if (WiimoteCollectionHelper.DiscoverDeviceIds(log).Count > 0)
        {
            log?.Invoke("[CONNECT] wake probe: HID appeared after enabling remembered HID service.");
            BluetoothDiagnostics.LogSnapshot(log, "wake-hid-after-enable", includeHidProbe: false);
            return;
        }

        if (WiimoteCollectionHelper.DiscoverDeviceIds(log).Count == 0)
        {
            log?.Invoke("[CONNECT] wake probe: no HID devices — Bluetooth inquiry for discoverable board.");
            var bt = PairDiscoverableBoard(log, cancellationToken, removeStalePairings: false);
            if (bt.Success)
            {
                log?.Invoke($"[CONNECT] wake probe: Bluetooth reconnect succeeded ({bt.DevicesPaired} device(s)).");
            }
            else if (HasRememberedNintendoDevices(log))
            {
                log?.Invoke(
                    "[CONNECT] wake probe: Windows still has a Nintendo pairing but inquiry found no discoverable board — " +
                    "press SYNC under the battery cover (or remove the board from Windows Bluetooth and pair again).");
            }
        }

        var woke = 0;
        for (var attempt = 1; attempt <= BalanceConstants.PostPairHidRetryAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (WiimoteCollectionHelper.DiscoverDeviceIds(log).Count > 0)
            {
                log?.Invoke("[CONNECT] wake probe: HID visible — skipping connect/disconnect ping.");
                BluetoothDiagnostics.LogSnapshot(log, "wake-hid-visible", includeHidProbe: false);
                return;
            }

            woke = WiimoteCollectionHelper.WakeDevices(log);
            if (woke > 0)
            {
                log?.Invoke($"[CONNECT] wake probe: brief wake on {woke} device(s).");
                Thread.Sleep(BalanceConstants.PostWakeSettleMs);
                BluetoothDiagnostics.LogSnapshot(log, "wake-after-ping", includeHidProbe: false);
                return;
            }

            if (attempt < BalanceConstants.PostPairHidRetryAttempts)
            {
                log?.Invoke($"[CONNECT] wake probe: retry {attempt}/{BalanceConstants.PostPairHidRetryAttempts - 1}…");
                Thread.Sleep(BalanceConstants.PostPairHidRetryMs);
            }
        }

        BluetoothDiagnostics.LogSnapshot(log, "wake-failed", includeHidProbe: false);
        log?.Invoke("[CONNECT] wake probe: no sessions opened (board may be asleep — press SYNC).");
    }

    /// <summary>FormBluetooth post-pair: wait for HID install, then Connect/LED/Disconnect wake ping.</summary>
    private static void RunFormBluetoothWakePing(Action<string>? log, CancellationToken cancellationToken)
    {
        log?.Invoke("[CONNECT] Post-pair: waiting for Windows HID enumeration (FormBluetooth 4s)…");
        if (cancellationToken.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(BalanceConstants.PostPairHidEnumerateMs)))
        {
            cancellationToken.ThrowIfCancellationRequested();
        }

        cancellationToken.ThrowIfCancellationRequested();
        var woke = WiimoteCollectionHelper.WakeDevices(log);
        log?.Invoke(
            woke > 0
                ? $"[CONNECT] Post-pair: FormBluetooth wake ping on {woke} device(s)."
                : "[CONNECT] Post-pair: wake ping found no HID yet (board may still be waking).");
    }

    private static List<BluetoothDeviceInfo> EnableHidOnRememberedNintendoDevices(
        Action<string>? log,
        CancellationToken cancellationToken)
    {
        var remembered = GetRememberedNintendoDevices(log);
        foreach (var device in remembered)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                device.SetServiceState(BluetoothService.HumanInterfaceDevice, true);
                log?.Invoke(
                    $"[CONNECT] wake probe: enabled HID on {device.DeviceName} " +
                    $"(addr={device.DeviceAddress} connected={device.Connected} auth={device.Authenticated}).");
            }
            catch (Exception ex)
            {
                log?.Invoke($"[CONNECT] wake probe: enable HID note ({device.DeviceName}): {ex.Message}");
            }
        }

        return remembered;
    }

    /// <summary>
    /// Re-establish Bluetooth link to a remembered board (linux bluetoothctl connect/trust equivalent).
    /// After permanent pairing, stepping on the board can wake it — this nudges Windows to reconnect HID.
    /// </summary>
    private void TryReconnectRememberedDevices(
        IReadOnlyList<BluetoothDeviceInfo> remembered,
        Action<string>? log,
        CancellationToken cancellationToken)
    {
        var hostMac = TryGetLocalAdapterMac();
        if (hostMac is null || !WiiBluetoothPin.TryCreateFromHostMac(hostMac, out var pin, out _))
        {
            log?.Invoke("[BT] reconnect: skipped (adapter or Wii PIN unavailable).");
            return;
        }

        foreach (var device in remembered)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (device.Connected && device.Authenticated)
            {
                log?.Invoke($"[BT] reconnect: {device.DeviceName} already connected/authenticated.");
                continue;
            }

            log?.Invoke(
                $"[BT] reconnect: restoring link to {device.DeviceName} " +
                $"(stand on board or press power — no SYNC needed if already paired)…");

            try
            {
                if (TryPairRequestWithTimeout(device.DeviceAddress, pin, cancellationToken))
                {
                    device.SetServiceState(BluetoothService.HumanInterfaceDevice, true);
                    log?.Invoke($"[BT] reconnect: link restored on {device.DeviceName}.");
                    // #region agent log
                    AgentDebugLog.Write(
                        "H17",
                        "BluetoothPairingService.TryReconnectRememberedDevices",
                        "reconnect succeeded",
                        new { device = device.DeviceName });
                    // #endregion
                }
                else
                {
                    log?.Invoke(
                        $"[BT] reconnect: timed out ({BalanceConstants.BluetoothReLinkTimeoutSeconds}s) — " +
                        "board may be asleep; press SYNC if HID never appears.");
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                log?.Invoke($"[BT] reconnect: note ({device.DeviceName}): {ex.Message}");
            }
        }
    }

    private static bool TryPairRequestWithTimeout(
        BluetoothAddress address,
        string pin,
        CancellationToken cancellationToken)
    {
        var pairTask = Task.Run(() =>
        {
            _ = new BluetoothWin32Authentication(address, pin);
            BluetoothSecurity.PairRequest(address, null);
        }, cancellationToken);

        var timeoutMs = BalanceConstants.BluetoothReLinkTimeoutSeconds * 1000;
        if (pairTask.Wait(timeoutMs, cancellationToken))
        {
            if (pairTask.IsFaulted)
            {
                throw pairTask.Exception?.InnerException ?? pairTask.Exception!;
            }

            return true;
        }

        return false;
    }

    private static List<BluetoothDeviceInfo> GetRememberedNintendoDevices(Action<string>? log)
    {
        try
        {
            using var btClient = new BluetoothClient();
            btClient.InquiryLength = TimeSpan.FromSeconds(1);
            return btClient.DiscoverDevices(255, false, true, false)
                .Where(IsNintendoDevice)
                .ToList();
        }
        catch (Exception ex)
        {
            log?.Invoke($"[CONNECT] wake probe: remembered-device note: {ex.Message}");
            return [];
        }
    }

    private static bool IsNintendoDevice(BluetoothDeviceInfo device) =>
        device.DeviceName.Contains("Nintendo", StringComparison.OrdinalIgnoreCase);

    private static BluetoothPairingResult Fail(string message) =>
        new() { Success = false, Message = message };
}
