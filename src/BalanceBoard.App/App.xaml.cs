using System.Windows;
using System.Windows.Threading;
using BalanceBoard.App.Services;
using BalanceBoard.Core.Services;

namespace BalanceBoard.App;

public partial class App : Application
{
    static App()
    {
        // WiimoteLib can throw on the thread pool after HID dispose; do not tear down the process.
        AppContext.SetSwitch("Switch.System.Threading.ThrowOnUncaughtThreadPoolExceptions", false);
    }
    private MainWindow? _mainWindow;
    private StartupOptions _options = new();
    private readonly FileLogService _fileLog = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        _options = StartupOptions.Parse(e.Args);
        GlobalExceptionLogging.Register(_fileLog);
        _fileLog.Write("Application starting.", "SESSION");

        if (!_options.SkipSingleInstance && !SingleInstanceService.TryBecomePrimary())
        {
            _fileLog.Write("Second instance detected — exiting.", "SESSION");
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        SessionEnding += (_, _) => ReleaseResources();

        try
        {
            _mainWindow = new MainWindow(_options, _fileLog);
            MainWindow = _mainWindow;
            _mainWindow.Show();
        }
        catch (Exception ex)
        {
            _fileLog.WriteException(ex, "Startup");
            MessageBox.Show(
                $"Failed to open the main window:\n{ex.Message}\n\nDetails were written to:\n{_fileLog.CurrentLogPath}",
                "Balance Board Controller",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
            return;
        }

        if (!_options.SkipSingleInstance)
        {
            try
            {
                SingleInstanceService.StartActivationListener(_mainWindow);
            }
            catch (Exception ex)
            {
                _fileLog.WriteException(ex, "SingleInstance listener");
            }
        }

        _ = RunDeferredStartupAsync();

        base.OnStartup(e);
    }

    private async Task RunDeferredStartupAsync()
    {
        try
        {
            var killed = 0;
            if (!_options.SkipProcessCleanup)
            {
                await Task.Run(() =>
                {
                    try
                    {
                        killed = FeederProcessCleanup.TerminateCompetingFeeders();
                        FeederProcessCleanup.WaitForVJoyDeviceFree(1, timeoutMs: 2000);
                    }
                    catch (Exception ex)
                    {
                        _fileLog.WriteException(ex, "Startup cleanup");
                    }
                });
            }

            if (_mainWindow is null)
            {
                return;
            }

            await _mainWindow.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    _mainWindow.RunDeferredStartup(_options.ConnectOnLaunch, killed);
                }
                catch (Exception ex)
                {
                    _fileLog.WriteException(ex, "Deferred startup");
                }
            });
        }
        catch (Exception ex)
        {
            _fileLog.WriteException(ex, "Deferred startup outer");
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _fileLog.WriteException(e.Exception, "UI thread");
        GlobalExceptionLogging.WriteFatal(e.Exception, "UI thread");
        MessageBox.Show(
            $"Unexpected error:\n{e.Exception.Message}\n\nThe app will keep running.\nDetails: {_fileLog.CurrentLogPath}",
            "Balance Board Controller",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _fileLog.WriteShutdown($"exit code {e.ApplicationExitCode}");
        SingleInstanceService.Stop();
        ReleaseResources();
        base.OnExit(e);
    }

    private void ReleaseResources()
    {
        try { _mainWindow?.ForceShutdown(); }
        catch (Exception ex) { _fileLog.WriteException(ex, "Shutdown"); }
    }
}
