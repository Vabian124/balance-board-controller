using BalanceBoard.Core.Models;
using BalanceBoard.Core.Services;
using BalanceBoard.Testing;
using Xunit;

namespace BalanceBoard.Integration.Tests;

public class ConnectionWorkerTests
{
    [Fact]
    public void Invoke_runs_on_worker_thread()
    {
        using var worker = new ConnectionWorker();
        var apartment = worker.InvokeStrict(() => Thread.CurrentThread.GetApartmentState());
        Assert.Equal(ApartmentState.STA, apartment);
    }

    [Fact]
    public void Poll_tick_runs_while_polling_enabled()
    {
        using var worker = new ConnectionWorker();
        var ticks = 0;
        worker.SetPollTick(() => Interlocked.Increment(ref ticks));
        worker.StartPolling();
        Thread.Sleep(180);
        worker.StopPolling();
        Assert.True(ticks >= 2);
    }
}

public class ConnectFlowTests
{
    private static BalanceBoardSession CreateSession(
        FakeBalanceBoardConnection connection,
        FakeBluetoothPairingService pairing,
        AppSettings? settings = null)
    {
        var session = new BalanceBoardSession(
            gameController: new NullGameControllerOutput(),
            actionSimulator: new NullActionSimulator(),
            connection: connection,
            pairing: pairing);
        if (settings is not null)
        {
            session.LoadSettings(settings, initializeVJoy: false);
        }

        return session;
    }

    [Fact]
    public async Task QuickReconnect_succeeds_when_hid_available()
    {
        using var session = CreateSession(new FakeBalanceBoardConnection(), new FakeBluetoothPairingService());
        var result = await session.ConnectWithIntentAsync(ConnectionIntent.QuickReconnect);
        Assert.True(result.IsSuccess);
        Assert.True(session.IsConnected);
    }

    [Fact]
    public async Task QuickReconnect_fails_when_no_devices()
    {
        var connection = new FakeBalanceBoardConnection { DiscoveredDevices = Array.Empty<string>() };
        using var session = CreateSession(connection, new FakeBluetoothPairingService());
        var result = await session.ConnectWithIntentAsync(ConnectionIntent.QuickReconnect);
        Assert.False(result.IsSuccess);
        Assert.Equal(ConnectStatus.NoDevices, result.Status);
    }

    [Fact]
    public async Task PairAndConnect_succeeds_after_pairing()
    {
        var pairing = new FakeBluetoothPairingService();
        pairing.EnqueuePairResult(new BluetoothPairingResult
        {
            Success = true,
            Message = "Paired 1 Nintendo device(s).",
            DevicesPaired = 1,
        });

        var connection = new FakeBalanceBoardConnection
        {
            ConnectHandler = _ => pairing.PairCallCount > 0,
        };
        using var session = CreateSession(connection, pairing);
        var result = await session.ConnectWithIntentAsync(ConnectionIntent.PairAndConnect, discoveryRounds: 1);
        Assert.True(result.IsSuccess);
        Assert.Equal(1, pairing.PairCallCount);
    }

    [Fact]
    public async Task PairAndConnect_fails_when_pairing_exhausted()
    {
        var pairing = new FakeBluetoothPairingService();
        pairing.EnqueuePairResult(new BluetoothPairingResult { Success = false, Message = "No Nintendo device found." });

        var connection = new FakeBalanceBoardConnection { ConnectHandler = _ => false };
        using var session = CreateSession(connection, pairing);
        var result = await session.ConnectWithIntentAsync(ConnectionIntent.PairAndConnect, discoveryRounds: 1);
        Assert.False(result.IsSuccess);
        Assert.Equal(ConnectStatus.PairingFailed, result.Status);
    }

    [Fact]
    public async Task PairAndConnect_fails_when_paired_but_hid_missing()
    {
        var connection = new FakeBalanceBoardConnection { DiscoveredDevices = Array.Empty<string>() };
        var pairing = new FakeBluetoothPairingService();
        pairing.EnqueuePairResult(new BluetoothPairingResult
        {
            Success = true,
            Message = "Paired 1 Nintendo device(s).",
            DevicesPaired = 1,
        });

        using var session = CreateSession(connection, pairing);
        var result = await session.ConnectWithIntentAsync(ConnectionIntent.PairAndConnect, discoveryRounds: 1);
        Assert.False(result.IsSuccess);
        Assert.Equal(ConnectStatus.HidFailed, result.Status);
    }

    [Fact]
    public async Task Connect_cancelled_returns_cancelled_status()
    {
        using var session = CreateSession(new FakeBalanceBoardConnection(), new FakeBluetoothPairingService());
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var result = await session.ConnectWithIntentAsync(ConnectionIntent.PairAndConnect, cancellationToken: cts.Token);
        Assert.Equal(ConnectStatus.Cancelled, result.Status);
    }

    [Fact]
    public async Task Connect_exception_returns_error_status()
    {
        var connection = new FakeBalanceBoardConnection
        {
            ConnectException = new InvalidOperationException("HID boom"),
        };
        using var session = CreateSession(connection, new FakeBluetoothPairingService());
        var result = await session.ConnectWithIntentAsync(ConnectionIntent.QuickReconnect);
        Assert.Equal(ConnectStatus.Error, result.Status);
        Assert.Contains("HID boom", result.Message);
    }

    [Fact]
    public async Task Simulated_board_polls_readings()
    {
        using var session = new BalanceBoardSession(
            gameController: new NullGameControllerOutput(),
            actionSimulator: new NullActionSimulator(),
            connection: new SimulatedBalanceBoardConnection());

        ProcessedBalance? seen = null;
        session.Processed += data => seen = data;

        var result = await session.ConnectWithIntentAsync(ConnectionIntent.QuickReconnect);
        Assert.True(result.IsSuccess);

        await Task.Delay(200);
        Assert.NotNull(seen);
        Assert.True(seen!.WeightKg > 0);
    }

    [Fact]
    public async Task Connect_logs_first_balance_reading()
    {
        using var session = CreateSession(new FakeBalanceBoardConnection(), new FakeBluetoothPairingService());
        var lines = new List<string>();
        session.Log += lines.Add;

        var result = await session.ConnectWithIntentAsync(ConnectionIntent.QuickReconnect);
        Assert.True(result.IsSuccess);

        await Task.Delay(200);
        Assert.Contains(
            lines,
            line => line.Contains("[CONNECT] First balance reading", StringComparison.Ordinal));
    }

    [Fact]
    public void Session_applies_profile_presets_without_forcing_game_on_relaunch()
    {
        using var session = CreateSession(new FakeBalanceBoardConnection(), new FakeBluetoothPairingService());
        session.ApplyKeyboardPreset();
        Assert.Equal(ActionPresets.KeyboardMovement, session.Settings.ActiveProfileName);
        Assert.False(session.Settings.EnableVJoy);

        session.ApplyPedalPreset();
        Assert.Equal(ActionPresets.Pedal, session.Settings.ActiveProfileName);
        Assert.True(session.Settings.SendLoadSensorsToAxes);
    }

    [Fact]
    public async Task Disconnect_stops_polling_without_crash()
    {
        using var session = CreateSession(new FakeBalanceBoardConnection(), new FakeBluetoothPairingService());
        var lines = new List<string>();
        session.Log += lines.Add;

        await session.ConnectWithIntentAsync(ConnectionIntent.QuickReconnect);
        session.Disconnect();

        Assert.False(session.IsConnected);
        Assert.Contains(lines, line => line.Contains("[DISCONNECT]", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Disconnect_survives_late_reading_callback()
    {
        var connection = new FakeBalanceBoardConnection { FireReadingAfterDisconnect = true };
        using var session = CreateSession(connection, new FakeBluetoothPairingService());
        var lines = new List<string>();
        session.Log += lines.Add;

        await session.ConnectWithIntentAsync(ConnectionIntent.QuickReconnect);
        session.Disconnect();

        await Task.Delay(100);
        Assert.False(session.IsConnected);
        Assert.Equal(1, connection.DisconnectCount);
    }

    [Fact]
    public async Task Auto_connect_path_wakes_paired_devices_when_hid_not_visible()
    {
        var pairing = new FakeBluetoothPairingService();
        var connection = new FakeBalanceBoardConnection { DiscoveredDevices = Array.Empty<string>() };
        using var session = CreateSession(connection, pairing);
        var result = await session.ConnectWithIntentAsync(ConnectionIntent.QuickReconnect);
        Assert.False(result.IsSuccess);
        Assert.True(pairing.WakeCallCount >= 1);
    }

    [Fact]
    public async Task QuickReconnect_skips_wake_when_hid_already_visible()
    {
        var pairing = new FakeBluetoothPairingService();
        var connection = new FakeBalanceBoardConnection();
        var lines = new List<string>();
        using var session = CreateSession(connection, pairing);
        session.Log += lines.Add;

        var result = await session.ConnectWithIntentAsync(ConnectionIntent.QuickReconnect);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, pairing.WakeCallCount);
        Assert.Contains(lines, line => line.Contains("fast path", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PairAndConnect_retries_hid_after_pair_when_board_still_booting()
    {
        var pairing = new FakeBluetoothPairingService();
        pairing.EnqueuePairResult(new BluetoothPairingResult
        {
            Success = true,
            Message = "Paired 1 Nintendo device(s).",
            DevicesPaired = 1,
        });

        var connectCalls = 0;
        var connection = new FakeBalanceBoardConnection
        {
            ConnectHandler = _ => ++connectCalls >= 3,
        };
        using var session = CreateSession(connection, pairing);
        var lines = new List<string>();
        session.Log += lines.Add;

        var result = await session.ConnectWithIntentAsync(ConnectionIntent.PairAndConnect, discoveryRounds: 1);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, connectCalls);
        Assert.Contains(lines, line => line.Contains("HID not ready yet", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PairAndConnect_runs_post_pair_hid_wake_after_successful_pair()
    {
        var pairing = new FakeBluetoothPairingService();
        pairing.EnqueuePairResult(new BluetoothPairingResult
        {
            Success = true,
            Message = "Paired 1 Nintendo device(s).",
            DevicesPaired = 1,
        });

        var connectCalls = 0;
        var connection = new FakeBalanceBoardConnection
        {
            ConnectHandler = _ => ++connectCalls >= 2,
        };
        using var session = CreateSession(connection, pairing);
        var lines = new List<string>();
        session.Log += lines.Add;

        var result = await session.ConnectWithIntentAsync(ConnectionIntent.PairAndConnect, discoveryRounds: 1);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, pairing.WakeCallCount);
        Assert.Contains(lines, line => line.Contains("Post-pair", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PairAndConnect_before_board_on_fails_without_exception()
    {
        var pairing = new FakeBluetoothPairingService();
        pairing.EnqueuePairResult(new BluetoothPairingResult
        {
            Success = false,
            Message = "No Nintendo device found. Press SYNC on the board and try again.",
        });

        var connection = new FakeBalanceBoardConnection { DiscoveredDevices = Array.Empty<string>() };
        using var session = CreateSession(connection, pairing);
        var result = await session.ConnectWithIntentAsync(ConnectionIntent.PairAndConnect, discoveryRounds: 1);
        Assert.False(result.IsSuccess);
        Assert.Equal(ConnectStatus.PairingFailed, result.Status);
        Assert.False(session.IsConnected);
    }

    [Fact]
    public async Task Connect_waits_when_bluetooth_unavailable_then_cancels()
    {
        var pairing = new FakeBluetoothPairingService { BluetoothAvailable = false };
        using var session = CreateSession(new FakeBalanceBoardConnection(), pairing);
        var lines = new List<string>();
        session.Log += lines.Add;

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(400);

        var result = await session.ConnectWithIntentAsync(
            ConnectionIntent.PairAndConnect,
            discoveryRounds: 1,
            cancellationToken: cts.Token);

        Assert.Equal(ConnectStatus.Cancelled, result.Status);
        Assert.Contains(lines, line => line.Contains("radio unavailable", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Connect_while_already_in_progress_returns_already_in_progress()
    {
        var pairing = new FakeBluetoothPairingService { BluetoothAvailable = false };
        using var session = CreateSession(new FakeBalanceBoardConnection(), pairing);

        var first = session.ConnectWithIntentAsync(ConnectionIntent.PairAndConnect, discoveryRounds: 1);
        await Task.Delay(50);
        var second = await session.ConnectWithIntentAsync(ConnectionIntent.PairAndConnect, discoveryRounds: 1);

        Assert.Equal(ConnectStatus.AlreadyInProgress, second.Status);
        session.CancelConnect();
        var firstResult = await first;
        Assert.Equal(ConnectStatus.Cancelled, firstResult.Status);
    }

    [Fact]
    public async Task Connect_waits_through_bluetooth_toggle_at_start()
    {
        var pairing = new FakeBluetoothPairingService { BluetoothAvailable = false };
        pairing.EnqueuePairResult(new BluetoothPairingResult
        {
            Success = true,
            Message = "Paired 1 Nintendo device(s).",
            DevicesPaired = 1,
        });

        var connection = new FakeBalanceBoardConnection { ConnectHandler = _ => true };
        using var session = CreateSession(connection, pairing);
        var lines = new List<string>();
        session.Log += lines.Add;

        var connectTask = session.ConnectWithIntentAsync(ConnectionIntent.PairAndConnect, discoveryRounds: 1);
        await Task.Delay(300);
        pairing.BluetoothAvailable = true;

        var result = await connectTask;
        Assert.True(result.IsSuccess);
        Assert.Contains(lines, line => line.Contains("radio unavailable", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(lines, line => line.Contains("radio available", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Bluetooth_recovery_pauses_when_radio_unavailable_then_resumes()
    {
        const string boardId = "FAKE-BOARD-001";
        var connection = new FakeBalanceBoardConnection { DiscoveredDevices = [boardId] };
        var pairing = new FakeBluetoothPairingService { BluetoothAvailable = true };
        var settings = new AppSettings
        {
            AutoConnectOnStartup = true,
            LastConnectedDeviceId = boardId,
        };
        using var session = CreateSession(connection, pairing, settings);
        var lines = new List<string>();
        session.Log += lines.Add;

        await session.ConnectWithIntentAsync(ConnectionIntent.QuickReconnect);
        Assert.True(session.IsConnected);

        pairing.BluetoothAvailable = false;
        connection.SimulateDrop();
        await Task.Delay(300);
        Assert.Contains(lines, line => line.Contains("radio unavailable", StringComparison.OrdinalIgnoreCase));

        pairing.BluetoothAvailable = true;
        await Task.Delay(BalanceConstants.ReconnectInitialDelayMs + BalanceConstants.ConnectHealthGraceMs + 1200);

        Assert.True(session.IsConnected);
        Assert.Contains(lines, line => line.Contains("radio available", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Not_balance_board_device_fails_connect()
    {
        var connection = new FakeBalanceBoardConnection { ReturnNotBalanceBoard = true };
        using var session = CreateSession(connection, new FakeBluetoothPairingService());
        var result = await session.ConnectWithIntentAsync(ConnectionIntent.QuickReconnect);
        Assert.False(result.IsSuccess);
        Assert.False(session.IsConnected);
    }

    [Fact]
    public async Task Multiple_pairing_rounds_eventually_connects()
    {
        var pairing = new FakeBluetoothPairingService();
        pairing.EnqueuePairResult(new BluetoothPairingResult { Success = false, Message = "round 1 miss" });
        pairing.EnqueuePairResult(new BluetoothPairingResult
        {
            Success = true,
            Message = "Paired 1 Nintendo device(s).",
            DevicesPaired = 1,
        });

        var connection = new FakeBalanceBoardConnection
        {
            ConnectHandler = _ => pairing.PairCallCount >= 2,
        };
        using var session = CreateSession(connection, pairing);
        var result = await session.ConnectWithIntentAsync(ConnectionIntent.PairAndConnect, discoveryRounds: 2);
        Assert.True(result.IsSuccess);
        Assert.Equal(2, pairing.PairCallCount);
    }

    [Fact]
    public async Task IsConnected_requires_live_readings_not_hid_open_only()
    {
        var connection = new FakeBalanceBoardConnection { BlockReadings = true };
        using var session = CreateSession(connection, new FakeBluetoothPairingService());
        var result = await session.ConnectWithIntentAsync(ConnectionIntent.QuickReconnect);
        Assert.True(result.IsSuccess);
        Assert.Equal(ConnectionPhase.Connecting, session.ConnectionPhase);
        Assert.False(session.IsConnected);

        connection.BlockReadings = false;
        await Task.Delay(200);
        Assert.Equal(ConnectionPhase.Connected, session.ConnectionPhase);
        Assert.True(session.IsConnected);
    }

    [Fact]
    public async Task Stale_hid_triggers_bluetooth_recovery()
    {
        const string boardId = "FAKE-BOARD-001";
        var connection = new FakeBalanceBoardConnection { DiscoveredDevices = [boardId] };
        var settings = new AppSettings
        {
            AutoConnectOnStartup = true,
            LastConnectedDeviceId = boardId,
        };
        using var session = CreateSession(connection, new FakeBluetoothPairingService(), settings);
        await session.ConnectWithIntentAsync(ConnectionIntent.QuickReconnect);
        Assert.True(session.IsConnected);

        connection.BlockReadings = true;
        await Task.Delay(BalanceConstants.ReadingHealthTimeoutMs + 500);
        Assert.False(session.IsConnected);

        connection.BlockReadings = false;
        await Task.Delay(BalanceConstants.ReconnectInitialDelayMs + BalanceConstants.PostWakeSettleMs + 800);
        Assert.True(session.IsConnected);
    }

    [Fact]
    public async Task Bluetooth_recovery_reconnects_after_unexpected_drop()
    {
        const string boardId = "FAKE-BOARD-001";
        var connection = new FakeBalanceBoardConnection { DiscoveredDevices = [boardId] };
        var pairing = new FakeBluetoothPairingService();
        var settings = new AppSettings
        {
            AutoConnectOnStartup = true,
            HasConnectedBefore = true,
            LastConnectedDeviceId = boardId,
        };
        using var session = CreateSession(connection, pairing, settings);
        var lines = new List<string>();
        session.Log += lines.Add;

        var result = await session.ConnectWithIntentAsync(ConnectionIntent.QuickReconnect);
        Assert.True(result.IsSuccess);
        Assert.True(session.IsConnected);

        connection.SimulateDrop();
        await Task.Delay(BalanceConstants.ReconnectInitialDelayMs + BalanceConstants.PostWakeSettleMs + 800);

        Assert.True(session.IsConnected);
        Assert.Equal(ConnectionPhase.Connected, session.ConnectionPhase);
        Assert.Contains(lines, line => line.Contains("[CONNECT] Bluetooth recovery", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Manual_disconnect_does_not_start_bluetooth_recovery()
    {
        const string boardId = "FAKE-BOARD-001";
        var connection = new FakeBalanceBoardConnection { DiscoveredDevices = [boardId] };
        var settings = new AppSettings
        {
            AutoConnectOnStartup = true,
            LastConnectedDeviceId = boardId,
        };
        using var session = CreateSession(connection, new FakeBluetoothPairingService(), settings);
        await session.ConnectWithIntentAsync(ConnectionIntent.QuickReconnect);
        session.Disconnect();

        await Task.Delay(BalanceConstants.ReconnectInitialDelayMs + 500);
        Assert.Equal(ConnectionPhase.Offline, session.ConnectionPhase);
        Assert.False(session.IsConnected);
    }

    [Fact]
    public async Task QuickReconnect_prefers_last_connected_device_id()
    {
        const string boardId = "FAKE-BOARD-002";
        var connection = new FakeBalanceBoardConnection
        {
            DiscoveredDevices = ["FAKE-BOARD-001", boardId],
        };
        var settings = new AppSettings { LastConnectedDeviceId = boardId };
        using var session = CreateSession(connection, new FakeBluetoothPairingService(), settings);
        var result = await session.ConnectWithIntentAsync(ConnectionIntent.QuickReconnect);
        Assert.True(result.IsSuccess);
        Assert.Equal(boardId, session.ConnectedDeviceId);
    }

    [Fact]
    public async Task QuickReconnect_escalates_to_pairing_when_adapter_mac_changed()
    {
        const string boardId = "FAKE-BOARD-001";
        var connection = new FakeBalanceBoardConnection { DiscoveredDevices = [boardId] };
        var pairing = new FakeBluetoothPairingService
        {
            AdapterMac = "AABBCCDDEEFF",
        };
        var connectCalls = 0;
        connection.ConnectHandler = _ => ++connectCalls > 0;
        pairing.EnqueuePairResult(new BluetoothPairingResult
        {
            Success = true,
            Message = "Paired 1 Nintendo device(s).",
            DevicesPaired = 1,
        });

        var settings = new AppSettings
        {
            LastConnectedDeviceId = boardId,
            LastBluetoothAdapterMac = "001122334455",
        };
        using var session = CreateSession(connection, pairing, settings);
        var lines = new List<string>();
        session.Log += lines.Add;

        var result = await session.ConnectWithIntentAsync(ConnectionIntent.QuickReconnect);
        Assert.True(result.IsSuccess);
        Assert.Equal("AABBCCDDEEFF", session.Settings.LastBluetoothAdapterMac);
        Assert.Contains(lines, line => line.Contains("Adapter address changed", StringComparison.Ordinal));
        Assert.True(pairing.PairCallCount >= 1);
    }
}
