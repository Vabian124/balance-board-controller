using BalanceBoard.Core.Models;
using BalanceBoard.Core.Services;
using BalanceBoard.Testing;
using Xunit;

namespace BalanceBoard.Integration.Tests;

/// <summary>
/// Regression coverage for the two-poll-path race: for a real device, BalanceBoardSession.Poll()
/// is invoked both by ConnectionWorker's own idle-tick health poll and by WiimoteLib's HID read
/// thread firing IBalanceBoardConnection.ReadingAvailable. Neither BalanceProcessor's tare/jump
/// state nor ActionEngine's pressed-key state was ever synchronized against that, so two
/// concurrent Poll() calls could race. FakeBalanceBoardConnection.RaiseReadingAvailable() lets
/// tests simulate that second (foreign) thread without real hardware.
/// </summary>
public class PollConcurrencyTests
{
    [Fact]
    [Trait("Category", "Slow")]
    public async Task Poll_never_runs_concurrently_when_reading_events_race_the_worker_tick()
    {
        const string boardId = "FAKE-BOARD-001";
        var connection = new FakeBalanceBoardConnection { DiscoveredDevices = [boardId] };
        var vjoy = new ConcurrencyTrackingGameControllerOutput();
        using var session = new BalanceBoardSession(
            gameController: vjoy,
            actionSimulator: new NullActionSimulator(),
            connection: connection,
            pairing: new FakeBluetoothPairingService());
        session.LoadSettings(new AppSettings { EnableVJoy = true, PollIntervalMs = 5 }, initializeVJoy: false);

        var result = await session.ConnectWithIntentAsync(ConnectionIntent.QuickReconnect);
        Assert.True(result.IsSuccess);

        using var stopCts = new CancellationTokenSource();
        var hammer = Task.Run(() =>
        {
            while (!stopCts.IsCancellationRequested)
            {
                connection.RaiseReadingAvailable();
            }
        });

        await Task.Delay(400);
        stopCts.Cancel();
        try
        {
            await hammer;
        }
        catch (OperationCanceledException)
        {
            // Expected from the busy loop unwinding.
        }

        Assert.True(vjoy.UpdateCount > 0, "Expected at least one vJoy update from overlapping poll sources.");
        Assert.Equal(1, vjoy.MaxObservedConcurrency);
    }
}
