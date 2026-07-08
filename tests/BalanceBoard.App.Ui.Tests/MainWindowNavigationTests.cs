using System.Windows;
using BalanceBoard.Core.Models;
using Xunit;

namespace BalanceBoard.App.Ui.Tests;

public sealed class MainWindowNavigationTests : UiTestBase
{
    [Fact]
    public void Advanced_tab_hidden_in_simple_detail_level()
    {
        var window = Ctx.CreateWindow(seedSettings: new AppSettings { UiDetailLevel = UiDetailLevel.Simple });
        try
        {
            WpfTestHost.Invoke(() =>
            {
                Assert.Equal(Visibility.Collapsed, window.TestAdvancedTab.Visibility);
                window.TestDetailLevelCombo.SelectedIndex = (int)UiDetailLevel.Advanced;
                window.TestPumpDispatcher();
                Assert.Equal(Visibility.Visible, window.TestAdvancedTab.Visibility);
            });
        }
        finally
        {
            Ctx.CloseAll();
        }
    }

    [Fact]
    public void Detail_level_change_persists_to_settings_file()
    {
        var window = Ctx.CreateWindow(seedSettings: new AppSettings { UiDetailLevel = UiDetailLevel.Standard });
        try
        {
            WpfTestHost.Invoke(() =>
            {
                window.TestDetailLevelCombo.SelectedIndex = (int)UiDetailLevel.Advanced;
                window.TestPumpDispatcher();
            });

            var saved = Ctx.ReadPersistedSettings();
            Assert.Equal(UiDetailLevel.Advanced, saved.UiDetailLevel);
        }
        finally
        {
            Ctx.CloseAll();
        }
    }
}
