using BalanceBoard.Core.Models;
using Xunit;

namespace BalanceBoard.App.Ui.Tests;

public sealed class MainWindowConnectTests : UiTestBase
{
    [Fact]
    public async Task Simulated_deferred_startup_connects_without_user_input()
    {
        var window = Ctx.CreateWindow(simulateBoard: true, runDeferredStartup: true, connectOnLaunch: true);
        try
        {
            await Ctx.WaitForAsync(
                () => WpfTestHost.Invoke(() => window.TestSession.IsConnected),
                TimeSpan.FromSeconds(8));

            WpfTestHost.Invoke(() =>
            {
                Assert.Contains("connected", window.TestConnectionChipText.Text, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("[CONNECT]", window.TestLogBox.Text, StringComparison.Ordinal);
            });
        }
        finally
        {
            Ctx.CloseAll();
        }
    }

    [Fact]
    public void First_launch_shows_welcome_without_auto_pairing()
    {
        var window = Ctx.CreateWindow(
            simulateBoard: false,
            runDeferredStartup: true,
            seedSettings: new AppSettings { HasConnectedBefore = false });

        try
        {
            WpfTestHost.Invoke(() =>
            {
                var log = window.TestLogBox.Text;
                Assert.Contains("First launch", log, StringComparison.Ordinal);
                Assert.DoesNotContain("Searching for balance board", log, StringComparison.OrdinalIgnoreCase);
            });
        }
        finally
        {
            Ctx.CloseAll();
        }
    }

    [Fact]
    public void Health_check_button_runs_without_crash()
    {
        var window = Ctx.CreateWindow(seedSettings: new AppSettings { UiDetailLevel = UiDetailLevel.Advanced });
        try
        {
            WpfTestHost.Invoke(() =>
            {
                window.TestSelectTab(3);
                window.TestRunHealthCheck();
                window.TestPumpDispatcher();
                Assert.False(string.IsNullOrWhiteSpace(window.TestLogBox.Text));
            });
        }
        finally
        {
            Ctx.CloseAll();
        }
    }

}
