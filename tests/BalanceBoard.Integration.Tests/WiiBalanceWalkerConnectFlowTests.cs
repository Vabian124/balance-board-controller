using BalanceBoard.Core.Abstractions;
using BalanceBoard.Core.Models;
using BalanceBoard.Core.Services;
using BalanceBoard.Testing;
using Xunit;

namespace BalanceBoard.Integration.Tests;

/// <summary>
/// Automated reference-flow tests using scripted fake Bluetooth + fake HID.
/// Maps to WiiBalanceWalker FormMain (Connect) and FormBluetooth (pair/wake).
/// </summary>
public class WiiBalanceWalkerConnectFlowTests
{
    private static BalanceBoardSession CreateSession(
        FakeBalanceBoardConnection connection,
        IBluetoothPairingService pairing,
        AppSettings? settings = null)
    {
        var session = new BalanceBoardSession(
            gameController: new NullGameControllerOutput(),
            actionSimulator: new NullActionSimulator(),
            connection: connection,
            pairing: pairing);

        session.LoadSettings(settings ?? new AppSettings
        {
            HasConnectedBefore = true,
            LastConnectedDeviceId = "37A15347",
            LastConnectedAtUtc = DateTime.UtcNow.AddDays(-1),
        }, initializeVJoy: false);

        return session;
    }

    [Fact]
    public async Task FormMain_fast_path_connects_when_hid_already_visible()
    {
        var connection = new FakeBalanceBoardConnection { DiscoveredDevices = ["37A15347"] };
        var pairing = new ScriptedBluetoothPairingService(connection, ReferenceConnectScenario.FormMainHidVisible);

        using var session = CreateSession(connection, pairing);
        var lines = new List<string>();
        session.Log += lines.Add;

        var result = await session.ConnectWithIntentAsync(ConnectionIntent.QuickReconnect);

        Assert.True(result.IsSuccess);
        Assert.True(session.IsConnected);
        Assert.Equal("37A15347", session.ConnectedDeviceId);
        Assert.Equal(0, pairing.WakeCallCount);
        Assert.Contains(lines, l => l.Contains("fast path", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Stale_windows_pairing_fails_without_sync_inquiry_finds_phone_only()
    {
        var connection = new FakeBalanceBoardConnection { DiscoveredDevices = [] };
        var pairing = new ScriptedBluetoothPairingService(connection, ReferenceConnectScenario.StaleWindowsPairingNoSync)
        {
            RememberedNintendoDevices = true,
            PostPairSettleMs = 0,
        };

        using var session = CreateSession(connection, pairing);
        var lines = new List<string>();
        session.Log += lines.Add;

        var result = await session.ConnectWithIntentAsync(ConnectionIntent.QuickReconnect, discoveryRounds: 1);

        Assert.False(result.IsSuccess);
        Assert.False(session.IsConnected);
        Assert.True(pairing.WakeCallCount >= 1);
        Assert.Contains(lines, l => l.Contains("OPPO Reno Z", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("press SYNC", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(lines, l => l.Contains("Connected to", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Prepared_board_on_feet_reconnects_without_sync_inquiry()
    {
        var connection = new FakeBalanceBoardConnection { DiscoveredDevices = [] };
        var pairing = new ScriptedBluetoothPairingService(connection, ReferenceConnectScenario.PreparedBoardOnFeet)
        {
            RememberedNintendoDevices = true,
            PostPairSettleMs = 0,
        };

        using var session = CreateSession(connection, pairing);
        var lines = new List<string>();
        session.Log += lines.Add;

        var result = await session.ConnectWithIntentAsync(ConnectionIntent.QuickReconnect);

        Assert.True(result.IsSuccess);
        Assert.True(session.IsConnected);
        Assert.Equal(0, pairing.PairCallCount);
        Assert.Contains(lines, l => l.Contains("[BT] reconnect:", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, l => l.Contains("Searching for balance board", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FormBluetooth_fresh_sync_pair_reveals_hid_then_connects()
    {
        var connection = new FakeBalanceBoardConnection { DiscoveredDevices = [] };
        var pairing = new ScriptedBluetoothPairingService(connection, ReferenceConnectScenario.FormBluetoothFreshSyncPair)
        {
            RememberedNintendoDevices = true,
            PostPairSettleMs = 0,
        };

        using var session = CreateSession(connection, pairing);
        var lines = new List<string>();
        session.Log += lines.Add;

        var result = await session.ConnectWithIntentAsync(ConnectionIntent.QuickReconnect, discoveryRounds: 1);

        Assert.True(result.IsSuccess);
        Assert.True(session.IsConnected);
        Assert.Equal("37A15347", session.ConnectedDeviceId);
        Assert.True(pairing.WakeCallCount >= 1);
        Assert.True(pairing.PairCallCount >= 1);
        Assert.Contains(lines, l => l.Contains("Pairing Nintendo RVL-WBC-01", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("FormBluetooth wake ping", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Deleted_pairing_then_sync_succeeds_on_later_wake_attempt()
    {
        var connection = new FakeBalanceBoardConnection { DiscoveredDevices = [] };
        var pairing = new ScriptedBluetoothPairingService(connection, ReferenceConnectScenario.DeletedPairingThenSync)
        {
            RememberedNintendoDevices = false,
            SyncDiscoverableAfterWakeCall = 1,
            PostPairSettleMs = 0,
        };

        using var session = CreateSession(connection, pairing, new AppSettings
        {
            HasConnectedBefore = true,
            LastConnectedDeviceId = null,
        });

        var result = await session.ConnectWithIntentAsync(ConnectionIntent.QuickReconnect, discoveryRounds: 1);

        Assert.True(result.IsSuccess);
        Assert.True(session.IsConnected);
        Assert.True(pairing.WakeCallCount >= 1);
    }

    [Fact]
    public async Task PairAndConnect_intent_runs_wake_before_try_connect_then_pairs()
    {
        var connection = new FakeBalanceBoardConnection { DiscoveredDevices = [] };
        var pairing = new ScriptedBluetoothPairingService(connection, ReferenceConnectScenario.FormBluetoothFreshSyncPair)
        {
            PostPairSettleMs = 0,
        };

        using var session = CreateSession(connection, pairing);
        var lines = new List<string>();
        session.Log += lines.Add;

        var result = await session.ConnectWithIntentAsync(ConnectionIntent.PairAndConnect, discoveryRounds: 1);

        Assert.True(result.IsSuccess);
        Assert.True(pairing.WakeCallCount >= 1);
        Assert.Contains(lines, l => l.Contains("Pairing Nintendo RVL-WBC-01", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("FormBluetooth wake ping", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Board_off_no_discoverable_escalates_to_pairing_failed()
    {
        var connection = new FakeBalanceBoardConnection { DiscoveredDevices = [] };
        var pairing = new ScriptedBluetoothPairingService(connection, ReferenceConnectScenario.BoardOffNoDiscoverable)
        {
            PostPairSettleMs = 0,
        };

        using var session = CreateSession(connection, pairing);
        var result = await session.ConnectWithIntentAsync(ConnectionIntent.QuickReconnect, discoveryRounds: 1);

        Assert.False(result.IsSuccess);
        Assert.Equal(ConnectStatus.PairingFailed, result.Status);
    }

    [Fact]
    public async Task Preferred_device_id_used_after_form_bluetooth_wake()
    {
        var connection = new FakeBalanceBoardConnection { DiscoveredDevices = [] };
        var pairing = new ScriptedBluetoothPairingService(connection, ReferenceConnectScenario.FormBluetoothFreshSyncPair)
        {
            RevealedDeviceId = "37A15347",
            PostPairSettleMs = 0,
        };

        using var session = CreateSession(connection, pairing, new AppSettings
        {
            HasConnectedBefore = true,
            LastConnectedDeviceId = "37A15347",
        });

        var result = await session.ConnectWithIntentAsync(ConnectionIntent.QuickReconnect, discoveryRounds: 1);

        Assert.True(result.IsSuccess);
        Assert.Equal("37A15347", session.ConnectedDeviceId);
    }

    [Fact]
    public async Task No_remembered_nintendo_escalates_to_full_pairing()
    {
        var connection = new FakeBalanceBoardConnection { DiscoveredDevices = [] };
        var pairing = new ScriptedBluetoothPairingService(connection, ReferenceConnectScenario.FormBluetoothFreshSyncPair)
        {
            RememberedNintendoDevices = false,
            PostPairSettleMs = 0,
        };

        using var session = CreateSession(connection, pairing, new AppSettings
        {
            HasConnectedBefore = true,
            LastConnectedDeviceId = "37A15347",
        });

        var lines = new List<string>();
        session.Log += lines.Add;

        var result = await session.ConnectWithIntentAsync(ConnectionIntent.QuickReconnect, discoveryRounds: 1);

        Assert.True(result.IsSuccess);
        Assert.True(session.IsConnected);
        Assert.Contains(lines, l => l.Contains("starting full pairing", StringComparison.OrdinalIgnoreCase));
        Assert.True(pairing.PairCallCount >= 1);
    }

    [Fact]
    public void PairDiscoverableBoard_removeStalePairings_invokes_stale_cleanup()
    {
        var connection = new FakeBalanceBoardConnection { DiscoveredDevices = [] };
        var pairing = new ScriptedBluetoothPairingService(connection, ReferenceConnectScenario.FormBluetoothFreshSyncPair)
        {
            PostPairSettleMs = 0,
        };

        var lines = new List<string>();
        var result = pairing.PairDiscoverableBoard(lines.Add, removeStalePairings: true);

        Assert.True(result.Success);
        Assert.Equal(1, pairing.RemoveStalePairingsCallCount);
        Assert.Contains(lines, l => l.Contains("Removing stale Nintendo pairings", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("FormBluetooth wake ping", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task QuickReconnect_escalates_to_full_pairing_with_stale_removal()
    {
        var connection = new FakeBalanceBoardConnection { DiscoveredDevices = [] };
        var pairing = new ScriptedBluetoothPairingService(connection, ReferenceConnectScenario.BoardOffNoDiscoverable)
        {
            PostPairSettleMs = 0,
            RememberedNintendoDevices = true,
            EscalationScenario = ReferenceConnectScenario.FormBluetoothFreshSyncPair,
            EscalateScenarioAfterPairCall = int.MaxValue,
        };

        using var session = CreateSession(connection, pairing);
        var lines = new List<string>();
        session.Log += lines.Add;

        var result = await session.ConnectWithIntentAsync(ConnectionIntent.QuickReconnect, discoveryRounds: 1);

        Assert.True(result.IsSuccess);
        Assert.Contains(lines, l => l.Contains("full pairing", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, pairing.RemoveStalePairingsCallCount);
    }

    [Fact]
    public async Task Dynamic_hid_reveals_only_after_inline_wake_ping()
    {
        var connection = new FakeBalanceBoardConnection { DiscoveredDevices = [] };
        var pairing = new ScriptedBluetoothPairingService(connection, ReferenceConnectScenario.FormBluetoothFreshSyncPair)
        {
            PostPairSettleMs = 0,
            HidRevealAfterWakePingOnly = true,
        };

        using var session = CreateSession(connection, pairing);
        var lines = new List<string>();
        session.Log += lines.Add;

        Assert.Empty(connection.DiscoveredDevices);

        var result = await session.ConnectWithIntentAsync(ConnectionIntent.QuickReconnect, discoveryRounds: 1);

        Assert.True(result.IsSuccess);
        Assert.Single(connection.DiscoveredDevices);
        Assert.True(WiiBalanceWalkerReferenceSteps.ContainsInOrder(lines,
        [
            "Pairing Nintendo RVL-WBC-01",
            "Thread.Sleep 4000",
            "FormBluetooth wake ping",
        ]));
    }

    [Fact]
    public void Reference_form_bluetooth_steps_match_scripted_pair_wake_log()
    {
        var connection = new FakeBalanceBoardConnection { DiscoveredDevices = [] };
        var pairing = new ScriptedBluetoothPairingService(connection, ReferenceConnectScenario.FormBluetoothFreshSyncPair)
        {
            PostPairSettleMs = 0,
        };

        var lines = new List<string>();
        pairing.PairDiscoverableBoard(lines.Add);

        Assert.True(WiiBalanceWalkerReferenceSteps.ContainsInOrder(lines, WiiBalanceWalkerReferenceSteps.FormBluetoothPairWake));
    }
}
