using System.IO;
using System.IO.Pipes;
using System.Windows;

namespace BalanceBoard.App.Services;

/// <summary>
/// Ensures one app instance; second launch activates the running window.
/// </summary>
public static class SingleInstanceService
{
    public const string PipeName = "BalanceBoardApp_SingleInstance";
    public const string ActivateMessage = "activate";
    private const string MutexName = "BalanceBoardApp_SingleInstance_Mutex";

    private static CancellationTokenSource? _listenerCts;
    private static Mutex? _heldMutex;

    public static bool TryBecomePrimary()
    {
        try
        {
            var mutex = new Mutex(true, MutexName, out var createdNew);
            if (!createdNew)
            {
                mutex.Dispose();
                SignalRunningInstance();
                return false;
            }

            _heldMutex = mutex;
            return true;
        }
        catch
        {
            return true;
        }
    }

    public static void StartActivationListener(Window window)
    {
        _listenerCts?.Cancel();
        _listenerCts = new CancellationTokenSource();
        var token = _listenerCts.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await using var server = new NamedPipeServerStream(
                        PipeName,
                        PipeDirection.In,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await server.WaitForConnectionAsync(token);
                    using var reader = new StreamReader(server);
                    var msg = await reader.ReadLineAsync(token);
                    if (msg == ActivateMessage)
                    {
                        await window.Dispatcher.InvokeAsync(() =>
                        {
                            if (window.WindowState == WindowState.Minimized)
                            {
                                window.WindowState = WindowState.Normal;
                            }

                            window.Activate();
                            window.Focus();

                            if (window is MainWindow main)
                            {
                                main.OnActivatedFromSecondInstance();
                            }
                        });
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    await Task.Delay(500, token);
                }
            }
        }, token);
    }

    public static void Stop()
    {
        _listenerCts?.Cancel();
        _listenerCts = null;

        try
        {
            _heldMutex?.ReleaseMutex();
        }
        catch
        {
            // ignored
        }

        _heldMutex?.Dispose();
        _heldMutex = null;
    }

    private static void SignalRunningInstance()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(1500);
            using var writer = new StreamWriter(client) { AutoFlush = true };
            writer.WriteLine(ActivateMessage);
        }
        catch
        {
            // Running instance is shutting down.
        }
    }
}
