using System.Collections.Concurrent;
using BalanceBoard.Core.Models;

namespace BalanceBoard.Core.Services;

/// <summary>
/// Single persistent STA thread for WiimoteLib and Bluetooth. Prevents OnReadData-after-dispose crashes.
/// </summary>
public sealed class ConnectionWorker : IDisposable
{
    private readonly BlockingCollection<Action> _queue = new();
    private readonly Thread _thread;
    private readonly object _pollSync = new();
    private Action? _pollTick;
    private volatile bool _pollEnabled;
    private volatile bool _disposed;

    public ConnectionWorker()
    {
        _thread = new Thread(WorkerLoop)
        {
            IsBackground = true,
            Name = "BalanceBoard-ConnectionWorker",
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    public void Invoke(Action action) => Run(action, rethrow: false);

    public void InvokeStrict(Action action) => Run(action, rethrow: true);

    public T InvokeStrict<T>(Func<T> func)
    {
        if (Thread.CurrentThread == _thread)
        {
            return func();
        }

        T? result = default;
        Run(() => result = func(), rethrow: true);
        return result!;
    }

    public bool TryInvoke<T>(Func<T> func, out T? result, T? fallback = default)
    {
        result = fallback;
        if (_disposed)
        {
            return false;
        }

        try
        {
            if (Thread.CurrentThread == _thread)
            {
                result = func();
                return true;
            }

            T? inner = default;
            Run(() => inner = func(), rethrow: false);
            result = inner;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void Run(Action action, bool rethrow)
    {
        if (_disposed)
        {
            return;
        }

        if (Thread.CurrentThread == _thread)
        {
            if (rethrow)
            {
                action();
                return;
            }

            try
            {
                action();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ConnectionWorker inline: {ex}");
            }

            return;
        }

        Exception? error = null;
        using var done = new ManualResetEventSlim(false);
        try
        {
            _queue.Add(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    error = ex;
                }
                finally
                {
                    done.Set();
                }
            });
        }
        catch (InvalidOperationException)
        {
            return;
        }

        done.Wait();
        if (error is not null)
        {
            if (rethrow)
            {
                throw error;
            }

            System.Diagnostics.Debug.WriteLine($"ConnectionWorker invoke: {error}");
        }
    }

    public void SetPollTick(Action? tick)
    {
        lock (_pollSync)
        {
            _pollTick = tick;
        }
    }

    public void StartPolling() => _pollEnabled = true;

    public void StopPolling() => _pollEnabled = false;

    private void WorkerLoop()
    {
        while (!_disposed)
        {
            while (_queue.TryTake(out var action, TimeSpan.FromMilliseconds(BalanceConstants.SessionPollIntervalMs)))
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ConnectionWorker: {ex}");
                }
            }

            if (_disposed)
            {
                break;
            }

            if (_pollEnabled)
            {
                Action? tick;
                lock (_pollSync)
                {
                    tick = _pollTick;
                }

                try
                {
                    tick?.Invoke();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ConnectionWorker poll: {ex}");
                }
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _pollEnabled = false;
        _queue.CompleteAdding();
        _thread.Join(TimeSpan.FromSeconds(5));
        _queue.Dispose();
    }
}
