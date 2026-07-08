using BalanceBoard.Core.Models;
using Xunit;

namespace BalanceBoard.App.Ui.Tests;

public sealed class MainWindowSettingsTests : UiTestBase
{
    [Fact]
    public void Deadzone_slider_change_persists_to_settings_file()
    {
        var window = Ctx.CreateWindow(seedSettings: new AppSettings
        {
            UiDetailLevel = UiDetailLevel.Advanced,
            UseSimpleSensitivity = false,
            DeadzonePercent = 5,
        });

        try
        {
            WpfTestHost.Invoke(() =>
            {
                window.TestSelectTab(2);
                window.TestDeadzoneSlider.Value = 12;
                window.TestPumpDispatcher();
            });

            var saved = Ctx.ReadPersistedSettings();
            Assert.Equal(12, saved.DeadzonePercent);
        }
        finally
        {
            Ctx.CloseAll();
        }
    }

    [Fact]
    public void Auto_connect_toggle_persists_to_settings_file()
    {
        var window = Ctx.CreateWindow(seedSettings: new AppSettings { AutoConnectOnStartup = false });
        try
        {
            WpfTestHost.Invoke(() =>
            {
                window.TestSelectTab(1);
                window.TestAutoConnectCheck.IsChecked = true;
                window.TestPumpDispatcher();
            });

            var saved = Ctx.ReadPersistedSettings();
            Assert.True(saved.AutoConnectOnStartup);
        }
        finally
        {
            Ctx.CloseAll();
        }
    }

    [Fact]
    public void Theme_change_persists_to_settings_file()
    {
        var window = Ctx.CreateWindow(seedSettings: new AppSettings
        {
            UiDetailLevel = UiDetailLevel.Standard,
            ThemePreference = ThemePreference.Light,
        });

        try
        {
            WpfTestHost.Invoke(() =>
            {
                window.TestSelectTab(1);
                window.TestThemeCombo.SelectedItem = ThemePreference.Dark;
                window.TestPumpDispatcher();
            });

            var saved = Ctx.ReadPersistedSettings();
            Assert.Equal(ThemePreference.Dark, saved.ThemePreference);
        }
        finally
        {
            Ctx.CloseAll();
        }
    }
}
