using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using BalanceBoard.Core.Services;

namespace BalanceBoard.App;

public partial class App : Application
{
    private const string SingleInstancePipeName = "BalanceBoardApp_SingleInstance";
    private Mutex? _singleInstanceMutex;
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        ShutdownMode = ShutdownMode.OnMainWindowClose;

        if (!TryAcquireSingleInstance())
        {
            TrySignalExistingInstance();
            Shutdown();
            return;
        }

        var killed = FeederProcessCleanup.TerminateCompetingFeeders();
        FeederProcessCleanup.WaitForVJoyDeviceFree(1);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        SessionEnding += OnSessionEnding;

        _mainWindow = new MainWindow(killed);
        MainWindow = _mainWindow;
        _mainWindow.Show();

        base.OnStartup(e);
    }

    private bool TryAcquireSingleInstance()
    {
        FeederProcessCleanup.TerminateCompetingFeeders(settleDelayMs: 250);

        _singleInstanceMutex = new Mutex(true, SingleInstancePipeName, out var createdNew);
        if (createdNew)
        {
            return true;
        }

        // Another instance won the race; stop it and take over (dev/debug friendly).
        FeederProcessCleanup.TerminateCompetingFeeders();
        Thread.Sleep(300);

        _singleInstanceMutex.Dispose();
        _singleInstanceMutex = new Mutex(true, SingleInstancePipeName, out createdNew);
        return createdNew;
    }

    private static void TrySignalExistingInstance()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", SingleInstancePipeName, PipeDirection.Out);
            client.Connect(500);
        }
        catch
        {
            // No listener — the other instance is shutting down.
        }
    }

    private void OnSessionEnding(object sender, SessionEndingCancelEventArgs e)
    {
        ReleaseResources();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ReleaseResources();
        MessageBox.Show(
            $"An unexpected error occurred:\n{e.Exception.Message}",
            "Balance Board Controller",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
        Shutdown(-1);
    }

    private void OnDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        ReleaseResources();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ReleaseResources();
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }

    private void ReleaseResources()
    {
        if (_mainWindow is null)
        {
            return;
        }

        try
        {
            _mainWindow.ForceShutdown();
        }
        catch
        {
            // ignored
        }
    }
}
