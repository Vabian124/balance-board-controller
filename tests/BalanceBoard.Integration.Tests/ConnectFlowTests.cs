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
        FakeBluetoothPairingService pairing)
    {
        return new BalanceBoardSession(
            gameController: new NullGameControllerOutput(),
            actionSimulator: new NullActionSimulator(),
            connection: connection,
            pairing: pairing);
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
    public async Task Disconnect_stops_polling_without_crash()
    {
        using var session = CreateSession(new FakeBalanceBoardConnection(), new FakeBluetoothPairingService());
        await session.ConnectWithIntentAsync(ConnectionIntent.QuickReconnect);
        session.Disconnect();
        Assert.False(session.IsConnected);
    }

    [Fact]
    public async Task Auto_connect_path_wakes_paired_devices()
    {
        var pairing = new FakeBluetoothPairingService();
        var attempts = 0;
        var connection = new FakeBalanceBoardConnection
        {
            ConnectHandler = _ => ++attempts >= 2,
        };
        using var session = CreateSession(connection, pairing);
        var result = await session.ConnectWithIntentAsync(ConnectionIntent.QuickReconnect);
        Assert.True(result.IsSuccess);
        Assert.Equal(1, pairing.WakeCallCount);
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
}
