using System.Windows;
using BalanceBoard.App.Services;
using Xunit;

namespace BalanceBoard.App.Ui.Tests;

public sealed class PhysicalTestModeTests : UiTestBase
{
    [Fact]
    public void StartupOptions_parse_physical_test_only_when_requested()
    {
        var normal = StartupOptions.Parse([]);
        Assert.False(normal.HardwareTestMode);
        Assert.Null(normal.PhysicalTestScenario);

        var physical = StartupOptions.Parse(["--physical-test", "connect-basic"]);
        Assert.True(physical.HardwareTestMode);
        Assert.Equal("connect-basic", physical.PhysicalTestScenario);
    }

    [Fact]
    public void Physical_test_panel_is_hidden_by_default_and_visible_when_requested()
    {
        var normalWindow = Ctx.CreateWindow();
        var physicalWindow = Ctx.CreateWindow(physicalTestScenario: "connect-basic");

        try
        {
            WpfTestHost.Invoke(() =>
            {
                Assert.Equal(Visibility.Collapsed, normalWindow.TestPhysicalTestPanelVisibility);
                Assert.Equal(Visibility.Visible, physicalWindow.TestPhysicalTestPanelVisibility);
                Assert.Contains("connect-basic", physicalWindow.TestPhysicalTestScenarioText, StringComparison.OrdinalIgnoreCase);
                Assert.Equal("Connect the board", physicalWindow.TestPhysicalTestStepTitle);
            });
        }
        finally
        {
            Ctx.CloseAll();
        }
    }
}
