using BalanceBoard.Core.Abstractions;
using BalanceBoard.Core.Models;
using BalanceBoard.Core.Services;

namespace BalanceBoard.Testing;

/// <summary>
/// Simulates WiiBalanceWalker Bluetooth pair/wake behavior without real hardware.
/// Drives <see cref="FakeBalanceBoardConnection"/> HID visibility to match reference timing.
/// </summary>
public sealed class ScriptedBluetoothPairingService(
    FakeBalanceBoardConnection connection,
    ReferenceConnectScenario scenario = ReferenceConnectScenario.FormBluetoothFreshSyncPair) : IBluetoothPairingService
{
    private readonly FakeBalanceBoardConnection _connection = connection;
    private readonly Queue<BluetoothPairingResult> _pairResults = new();

    public ReferenceConnectScenario Scenario { get; set; } = scenario;

    /// <summary>After <see cref="EscalateScenarioAfterPairCall"/> pair attempts, inquiry uses this scenario.</summary>
    public ReferenceConnectScenario? EscalationScenario { get; set; }

    public int EscalateScenarioAfterPairCall { get; set; } = 2;

    /// <summary>Device id revealed after FormBluetooth-style wake ping (default: real board from logs).</summary>
    public string RevealedDeviceId { get; set; } = "37A15347";

    /// <summary>Post-pair settle (reference FormBluetooth uses 4000 ms; tests use 0).</summary>
    public int PostPairSettleMs { get; set; }

    /// <summary>When true, HID stays hidden until RunFormBluetoothPostPairWake (dynamic HID-after-wake).</summary>
    public bool HidRevealAfterWakePingOnly { get; set; } = true;

    /// <summary>Wake call index before inquiry finds Nintendo (DeletedPairingThenSync).</summary>
    public int SyncDiscoverableAfterWakeCall { get; set; } = 3;

    public int WakeCallCount { get; private set; }
    public int PairCallCount { get; private set; }
    public int RemoveStalePairingsCallCount { get; private set; }
    public bool BluetoothAvailable { get; set; } = true;
    public string? AdapterMac { get; set; } = "A841F4E6F034";
    public bool RememberedNintendoDevices { get; set; } = true;

    public bool IsBluetoothAvailable() => BluetoothAvailable;

    public string? TryGetLocalAdapterMac() => AdapterMac;

    public bool HasRememberedNintendoDevices(Action<string>? log = null) =>
        RememberedNintendoDevices && Scenario is not ReferenceConnectScenario.DeletedPairingThenSync;

    public void EnqueuePairResult(BluetoothPairingResult result) => _pairResults.Enqueue(result);

    public void WakePairedDevices(Action<string>? log = null, CancellationToken cancellationToken = default)
    {
        WakeCallCount++;
        cancellationToken.ThrowIfCancellationRequested();

        log?.Invoke("[CONNECT] wake probe: starting paired-device wake sequence (v1.4 flow).");
        log?.Invoke("[SCRIPT] FormBluetooth: enable HID on remembered Nintendo devices");

        if (HasRememberedNintendoDevices(log))
        {
            log?.Invoke(
                "[CONNECT] wake probe: enabled HID on Nintendo RVL-WBC-01 " +
                "(addr=002331987181 connected=False auth=False).");
        }

        if (Scenario == ReferenceConnectScenario.PreparedBoardOnFeet)
        {
            log?.Invoke(
                "[BT] reconnect: restoring link to Nintendo RVL-WBC-01 " +
                "(stand on board or press power — no SYNC needed if already paired)…");
            log?.Invoke("[BT] reconnect: link restored on Nintendo RVL-WBC-01.");
            _connection.DiscoveredDevices = [RevealedDeviceId];
            log?.Invoke("[CONNECT] wake probe: HID appeared after enabling remembered HID service.");
            return;
        }

        if (PostPairSettleMs > 0)
        {
            if (cancellationToken.WaitHandle.WaitOne(PostPairSettleMs))
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        if (_connection.DiscoveredDevices.Count > 0)
        {
            log?.Invoke("[CONNECT] wake probe: HID visible — skipping connect/disconnect ping.");
            return;
        }

        log?.Invoke("[CONNECT] wake probe: no HID devices — Bluetooth inquiry for discoverable board.");
        var pair = PairDiscoverableBoard(log, cancellationToken, removeStalePairings: false);
        if (pair.Success)
        {
            log?.Invoke($"[CONNECT] wake probe: Bluetooth reconnect succeeded ({pair.DevicesPaired} device(s)).");
            return;
        }

        if (Scenario == ReferenceConnectScenario.StaleWindowsPairingNoSync && HasRememberedNintendoDevices(log))
        {
            log?.Invoke(
                "[CONNECT] wake probe: Windows still has a Nintendo pairing but inquiry found no discoverable board — " +
                "press SYNC under the battery cover (or remove the board from Windows Bluetooth and pair again).");
        }

        log?.Invoke("[CONNECT] wake probe: no sessions opened (board may be asleep — press SYNC).");
    }

    public BluetoothPairingResult PairDiscoverableBoard(
        Action<string>? log = null,
        CancellationToken cancellationToken = default,
        bool removeStalePairings = true)
    {
        PairCallCount++;
        cancellationToken.ThrowIfCancellationRequested();

        if (removeStalePairings)
        {
            RemoveStalePairingsCallCount++;
            log?.Invoke("[SCRIPT] FormBluetooth: RemoveExistingNintendoDevices");
            log?.Invoke("Removing stale Nintendo pairings…");
        }

        log?.Invoke("[SCRIPT] FormBluetooth: DiscoverDevices discoverable");
        log?.Invoke("Searching for balance board — press the red SYNC button under the battery cover.");

        var inquiry = SimulateInquiry(log, removeStalePairings);
        if (inquiry.NintendoFound)
        {
            log?.Invoke("[SCRIPT] FormBluetooth: PairRequest + SetServiceState HumanInterfaceDevice");
            log?.Invoke("Pairing Nintendo RVL-WBC-01 (002331987181)…");

            if (_pairResults.Count > 0)
            {
                var queued = _pairResults.Dequeue();
                log?.Invoke(queued.Message);
                if (queued.Success)
                {
                    RunFormBluetoothPostPairWake(log, cancellationToken);
                }

                return queued;
            }

            log?.Invoke("Finishing Bluetooth setup…");
            var result = new BluetoothPairingResult
            {
                Success = true,
                Message = "Paired 1 Nintendo device(s).",
                DevicesPaired = 1,
            };
            RunFormBluetoothPostPairWake(log, cancellationToken);
            return result;
        }

        log?.Invoke($"[BT] inquiry: discoverable={inquiry.TotalDevices}");
        foreach (var name in inquiry.NonNintendoNames)
        {
            log?.Invoke($"[BT]   [other] {name}");
        }

        if (_pairResults.Count > 0)
        {
            var queued = _pairResults.Dequeue();
            log?.Invoke(queued.Message);
            return queued;
        }

        return new BluetoothPairingResult
        {
            Success = false,
            Message = "No Nintendo device found. Press SYNC on the board and try again.",
        };
    }

    private void RunFormBluetoothPostPairWake(Action<string>? log, CancellationToken cancellationToken)
    {
        var settleMs = PostPairSettleMs > 0 ? PostPairSettleMs : BalanceConstants.PostPairHidEnumerateMs;
        log?.Invoke($"[SCRIPT] FormBluetooth: Thread.Sleep {settleMs}");
        log?.Invoke("[CONNECT] Post-pair: waiting for Windows HID enumeration (FormBluetooth 4s)…");
        if (PostPairSettleMs > 0 && cancellationToken.WaitHandle.WaitOne(PostPairSettleMs))
        {
            cancellationToken.ThrowIfCancellationRequested();
        }

        log?.Invoke("[SCRIPT] FormBluetooth: FindAllWiimotes → Connect → SetLEDs → Disconnect");
        _connection.DiscoveredDevices = [RevealedDeviceId];
        log?.Invoke($"[CONNECT] wake probe: 1 Wii HID device(s) found.");
        log?.Invoke($"[CONNECT] wake probe: HID ping id={RevealedDeviceId}");
        log?.Invoke("[CONNECT] wake probe: brief HID wake (LED only).");
        log?.Invoke("[CONNECT] Post-pair: FormBluetooth wake ping on 1 device(s).");
        log?.Invoke("[CONNECT] wake probe: brief wake on 1 device(s).");
        log?.Invoke($"[SCRIPT] Windows HID enumerated: {RevealedDeviceId}");
    }

    private InquirySimulation SimulateInquiry(Action<string>? log, bool removeStalePairings)
    {
        if (removeStalePairings && EscalationScenario is { } fullRound)
        {
            return fullRound switch
            {
                ReferenceConnectScenario.FormBluetoothFreshSyncPair =>
                    new InquirySimulation(true, 1, []),
                _ => new InquirySimulation(false, 0, []),
            };
        }

        var active = EscalationScenario is { } next && PairCallCount >= EscalateScenarioAfterPairCall
            ? next
            : Scenario;

        return active switch
        {
            ReferenceConnectScenario.FormBluetoothFreshSyncPair =>
                new InquirySimulation(true, 1, []),

            ReferenceConnectScenario.StaleWindowsPairingNoSync =>
                new InquirySimulation(false, 1, ["OPPO Reno Z"]),

            ReferenceConnectScenario.DeletedPairingThenSync when WakeCallCount >= SyncDiscoverableAfterWakeCall =>
                new InquirySimulation(true, 4, ["OPPO Reno Z"]),

            ReferenceConnectScenario.DeletedPairingThenSync =>
                new InquirySimulation(false, 1, ["OPPO Reno Z"]),

            ReferenceConnectScenario.BoardOffNoDiscoverable =>
                new InquirySimulation(false, 0, []),

            ReferenceConnectScenario.FormMainHidVisible =>
                new InquirySimulation(false, 0, []),

            _ => new InquirySimulation(false, 0, []),
        };
    }

    private readonly record struct InquirySimulation(
        bool NintendoFound,
        int TotalDevices,
        IReadOnlyList<string> NonNintendoNames);
}
