using System.Windows;
using System.Windows.Threading;
using BalanceBoard.App.Services;
using BalanceBoard.Core.Services;

namespace BalanceBoard.App;

public partial class App : Application
{
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

        _mainWindow = new MainWindow(_options, _fileLog);
        MainWindow = _mainWindow;
        _mainWindow.Show();

        if (!_options.SkipSingleInstance)
        {
            SingleInstanceService.StartActivationListener(_mainWindow);
        }

        _ = RunDeferredStartupAsync();

        base.OnStartup(e);
    }

    private async Task RunDeferredStartupAsync()
    {
        var killed = 0;
        if (!_options.SkipProcessCleanup)
        {
            await Task.Run(() =>
            {
                killed = FeederProcessCleanup.TerminateCompetingFeeders();
                FeederProcessCleanup.WaitForVJoyDeviceFree(1, timeoutMs: 2000);
            });
        }

        if (_mainWindow is null) return;

        await _mainWindow.Dispatcher.InvokeAsync(() =>
        {
            _mainWindow.RunDeferredStartup(_options.ConnectOnLaunch, killed);
        });
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _fileLog.WriteException(e.Exception, "FATAL UI thread");
        GlobalExceptionLogging.WriteFatal(e.Exception, "UI thread");
        ReleaseResources();
        MessageBox.Show(
            $"Unexpected error:\n{e.Exception.Message}\n\nDetails were written to:\n{_fileLog.CurrentLogPath}",
            "Balance Board Controller",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
        Shutdown(-1);
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
