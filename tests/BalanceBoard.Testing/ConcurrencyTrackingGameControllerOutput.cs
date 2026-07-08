using BalanceBoard.Core.Abstractions;
using BalanceBoard.Core.Models;

namespace BalanceBoard.Testing;

/// <summary>
/// Detects overlapping <see cref="Update"/> calls (no internal locking, unlike the real
/// vJoy adapter's lock) so tests can prove <c>BalanceBoardSession.Poll()</c> never runs
/// concurrently from two threads — e.g. the ConnectionWorker's idle-tick health poll racing
/// WiimoteLib's own HID read thread via <c>ReadingAvailable</c>.
/// </summary>
public sealed class ConcurrencyTrackingGameControllerOutput : IGameControllerOutput
{
    private int _active;
    private int _maxObservedConcurrency;
    private int _updateCount;

    public bool IsReady => true;

    public int MaxObservedConcurrency => _maxObservedConcurrency;

    public int UpdateCount => _updateCount;

    public bool Initialize(uint deviceId = 1, bool attemptCleanupOnBusy = true) => true;

    public void Update(ProcessedBalance data)
    {
        var current = Interlocked.Increment(ref _active);
        try
        {
            InterlockedMax(ref _maxObservedConcurrency, current);
            Interlocked.Increment(ref _updateCount);
            // Widen the race window: without Poll()'s reentrancy guard, a second thread
            // is very likely to enter Update() while this call is still "in flight".
            Thread.Sleep(15);
        }
        finally
        {
            Interlocked.Decrement(ref _active);
        }
    }

    private static void InterlockedMax(ref int location, int value)
    {
        int initial;
        do
        {
            initial = location;
            if (value <= initial)
            {
                return;
            }
        }
        while (Interlocked.CompareExchange(ref location, value, initial) != initial);
    }

    public void Center()
    {
    }

    public void Shutdown()
    {
    }

    public void Dispose()
    {
    }
}
