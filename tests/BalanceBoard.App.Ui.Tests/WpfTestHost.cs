using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using BalanceBoard.App;
using BalanceBoard.App.Services;
using BalanceBoard.Core.Models;
using BalanceBoard.Core.Services;

namespace BalanceBoard.App.Ui.Tests;

public sealed class WpfTestHost : IDisposable
{
    private static readonly Lazy<WpfTestHost> Shared = new(() => new WpfTestHost(), LazyThreadSafetyMode.ExecutionAndPublication);
    private static readonly TimeSpan DispatcherInvokeTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan DispatcherShutdownTimeout = TimeSpan.FromSeconds(5);

    private readonly Thread _thread;
    private readonly ManualResetEventSlim _ready = new(false);
    private bool _disposed;

    private WpfTestHost()
    {
        _thread = new Thread(() =>
        {
            try
            {
                Application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                Application.Resources.MergedDictionaries.Add(LoadTheme("Themes/Colors.Light.xaml"));
                Application.Resources.MergedDictionaries.Add(LoadTheme("Themes/Controls.xaml"));
                _ready.Set();
                Dispatcher.Run();
            }
            catch (Exception ex)
            {
                StartupException = ex;
                _ready.Set();
            }
        })
        {
            IsBackground = true,
            Name = "BalanceBoard.App.Ui.Tests",
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        if (!_ready.Wait(TimeSpan.FromSeconds(30)))
        {
            throw new TimeoutException("WPF test host failed to start within 30 seconds.");
        }

        if (StartupException is not null)
        {
            throw new InvalidOperationException("WPF test host failed to start.", StartupException);
        }
    }

    public static WpfTestHost Instance => Shared.Value;

    public Application Application { get; private set; } = null!;

    public Exception? StartupException { get; private set; }

    public static void Invoke(Action action) => Instance.InvokeCore(action);

    public static T Invoke<T>(Func<T> func) => Instance.InvokeCore(func);

    public static async Task InvokeAsync(Func<Task> action)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = Instance.Application.Dispatcher.BeginInvoke(async () =>
        {
            try
            {
                await action();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        await tcs.Task.WaitAsync(DispatcherInvokeTimeout).ConfigureAwait(false);
    }

    public static async Task<T> InvokeAsync<T>(Func<Task<T>> func)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = Instance.Application.Dispatcher.BeginInvoke(async () =>
        {
            try
            {
                tcs.SetResult(await func());
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return await tcs.Task.WaitAsync(DispatcherInvokeTimeout).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            if (Application is not null)
            {
                InvokeCore(() =>
                {
                    foreach (Window window in Application.Windows)
                    {
                        try
                        {
                            if (window is MainWindow main)
                            {
                                main.ForceShutdown();
                            }

                            window.Close();
                        }
                        catch
                        {
                            // Best-effort window cleanup before dispatcher shutdown.
                        }
                    }

                    Application.Dispatcher.InvokeShutdown();
                }, DispatcherShutdownTimeout);
            }

            _thread.Join(DispatcherShutdownTimeout);
        }
        catch
        {
            // Best-effort shutdown for test host teardown.
        }
    }

    private void InvokeCore(Action action)
        => InvokeCore(action, DispatcherInvokeTimeout);

    private void InvokeCore(Action action, TimeSpan timeout)
    {
        if (Application.Dispatcher.CheckAccess())
        {
            action();
            return;
        }

        var operation = Application.Dispatcher.InvokeAsync(action);
        if (!operation.Task.Wait(timeout))
        {
            throw new TimeoutException($"WPF dispatcher action did not complete within {timeout.TotalSeconds:0.#} seconds.");
        }

        operation.Task.GetAwaiter().GetResult();
    }

    private T InvokeCore<T>(Func<T> func)
    {
        if (Application.Dispatcher.CheckAccess())
        {
            return func();
        }

        var operation = Application.Dispatcher.InvokeAsync(func);
        if (!operation.Task.Wait(DispatcherInvokeTimeout))
        {
            throw new TimeoutException(
                $"WPF dispatcher function did not complete within {DispatcherInvokeTimeout.TotalSeconds:0.#} seconds.");
        }

        return operation.Task.GetAwaiter().GetResult();
    }

    private static ResourceDictionary LoadTheme(string relativePath) =>
        new()
        {
            Source = new Uri($"pack://application:,,,/BalanceBoardApp;component/{relativePath}", UriKind.Absolute),
        };
}

public sealed class UiTestContext : IDisposable
{
    private readonly List<MainWindow> _windows = [];

    public UiTestContext()
    {
        RootDir = Path.Combine(Path.GetTempPath(), "bbc-ui-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(RootDir);
        LogsDir = Path.Combine(RootDir, "logs");
        Directory.CreateDirectory(LogsDir);
        SettingsStore = new SettingsStore(RootDir);
        FileLog = new FileLogService(LogsDir);
    }

    public string RootDir { get; }

    public string LogsDir { get; }

    public SettingsStore SettingsStore { get; }

    public FileLogService FileLog { get; }

    public string SettingsPath => SettingsStore.SettingsPath;

    public MainWindow CreateWindow(
        bool simulateBoard = true,
        bool runDeferredStartup = false,
        bool connectOnLaunch = false,
        string? physicalTestScenario = null,
        AppSettings? seedSettings = null)
    {
        if (seedSettings is not null)
        {
            SettingsStore.Save(seedSettings);
        }

        var options = new StartupOptions
        {
            SkipProcessCleanup = true,
            SkipSingleInstance = true,
            SimulateBoard = simulateBoard,
            ConnectOnLaunch = connectOnLaunch,
            PhysicalTestScenario = physicalTestScenario,
        };

        var window = WpfTestHost.Invoke(() =>
        {
            var created = new MainWindow(options, FileLog, SettingsStore);
            if (runDeferredStartup)
            {
                created.RunDeferredStartup(connectOnLaunch);
            }

            return created;
        });

        _windows.Add(window);
        return window;
    }

    public AppSettings ReadPersistedSettings()
    {
        if (!File.Exists(SettingsPath))
        {
            throw new FileNotFoundException("Settings file missing.", SettingsPath);
        }

        var json = File.ReadAllText(SettingsPath);
        return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
    }

    public async Task WaitForAsync(Func<bool> predicate, TimeSpan timeout, TimeSpan? poll = null)
    {
        poll ??= TimeSpan.FromMilliseconds(50);
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (WpfTestHost.Invoke(predicate))
            {
                return;
            }

            await Task.Delay(poll.Value).ConfigureAwait(false);
        }

        throw new TimeoutException($"Condition not met within {timeout.TotalSeconds:0.0}s.");
    }

    public void CloseAll()
    {
        WpfTestHost.Invoke(() =>
        {
            foreach (var window in _windows)
            {
                try
                {
                    window.ForceShutdown();
                    window.Close();
                }
                catch
                {
                    // Window teardown should not fail the test host.
                }
            }
        });
        _windows.Clear();
    }

    public void Dispose()
    {
        CloseAll();
        try
        {
            if (Directory.Exists(RootDir))
            {
                Directory.Delete(RootDir, recursive: true);
            }
        }
        catch
        {
            // Temp cleanup is best-effort on Windows file locks.
        }
    }
}

public abstract class UiTestBase : IDisposable
{
    protected UiTestContext Ctx { get; } = new();

    public void Dispose() => Ctx.Dispose();
}
