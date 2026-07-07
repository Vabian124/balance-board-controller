using System.Reflection;
using BalanceBoard.Core.Abstractions;
using BalanceBoard.Core.Models;
using InTheHand.Net.Bluetooth;
using InTheHand.Net.Sockets;

namespace BalanceBoard.Core.Services;

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
        try
        {
            var radio = BluetoothRadio.PrimaryRadio;
            return radio is not null && radio.Mode != RadioMode.PowerOff;
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
            // HID appears after pairing without a connect/disconnect wake probe (that race crashes WiimoteLib).
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

    public void WakePairedDevices(Action<string>? log = null)
    {
        log?.Invoke("[CONNECT] wake probe: starting paired-device wake sequence.");

        EnableHidOnRememberedNintendoDevices(log);

        if (WiimoteCollectionHelper.DiscoverDeviceIds().Count == 0)
        {
            log?.Invoke("[CONNECT] wake probe: no HID devices — Bluetooth inquiry for discoverable board.");
            var bt = PairDiscoverableBoard(log, default, removeStalePairings: false);
            if (bt.Success)
            {
                log?.Invoke($"[CONNECT] wake probe: Bluetooth reconnect succeeded ({bt.DevicesPaired} device(s)).");
                Thread.Sleep(BalanceConstants.PostPairSettleMs);
            }
        }

        var woke = 0;
        for (var attempt = 1; attempt <= BalanceConstants.PostPairHidRetryAttempts; attempt++)
        {
            woke = WiimoteCollectionHelper.WakeDevices(log);
            if (woke > 0)
            {
                log?.Invoke($"[CONNECT] wake probe: held {woke} session(s) for {BalanceConstants.WakeProbeHoldMs} ms.");
                Thread.Sleep(BalanceConstants.PostWakeSettleMs);
                return;
            }

            if (attempt < BalanceConstants.PostPairHidRetryAttempts)
            {
                log?.Invoke($"[CONNECT] wake probe: retry {attempt}/{BalanceConstants.PostPairHidRetryAttempts - 1}…");
                Thread.Sleep(BalanceConstants.PostPairHidRetryMs);
            }
        }

        log?.Invoke("[CONNECT] wake probe: no sessions opened (board may be asleep).");
    }

    private static void EnableHidOnRememberedNintendoDevices(Action<string>? log)
    {
        try
        {
            using var btClient = new BluetoothClient();
            btClient.InquiryLength = TimeSpan.FromSeconds(1);
            var remembered = btClient.DiscoverDevices(255, false, true, false);
            foreach (var device in remembered)
            {
                if (!IsNintendoDevice(device))
                {
                    continue;
                }

                device.SetServiceState(BluetoothService.HumanInterfaceDevice, true);
                log?.Invoke($"[CONNECT] wake probe: enabled HID service on {device.DeviceName}.");
            }
        }
        catch (Exception ex)
        {
            log?.Invoke($"[CONNECT] wake probe: remembered-device note: {ex.Message}");
        }
    }

    private static bool IsNintendoDevice(BluetoothDeviceInfo device) =>
        device.DeviceName.Contains("Nintendo", StringComparison.OrdinalIgnoreCase);

    private static BluetoothPairingResult Fail(string message) =>
        new() { Success = false, Message = message };
}
