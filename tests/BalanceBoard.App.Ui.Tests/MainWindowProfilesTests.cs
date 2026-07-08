using BalanceBoard.Core.Models;
using BalanceBoard.Core.Services;
using Xunit;

namespace BalanceBoard.App.Ui.Tests;

public sealed class MainWindowProfilesTests : UiTestBase
{
    [Fact]
    public void Game_preset_button_updates_profile_and_persists()
    {
        var window = Ctx.CreateWindow(seedSettings: new AppSettings { ActiveProfileName = ActionPresets.KeyboardMovement });
        try
        {
            WpfTestHost.Invoke(() =>
            {
                window.TestSelectTab(1);
                window.TestClickGamePreset();
                window.TestPumpDispatcher();
            });

            var saved = Ctx.ReadPersistedSettings();
            Assert.Equal(ActionPresets.GameController, saved.ActiveProfileName);
            Assert.True(saved.EnableVJoy);
        }
        finally
        {
            Ctx.CloseAll();
        }
    }

    [Fact]
    public void Minecraft_preset_applies_without_styling_exception()
    {
        var window = Ctx.CreateWindow();
        try
        {
            WpfTestHost.Invoke(() =>
            {
                window.TestSelectTab(1);
                window.TestClickMinecraftPreset();
                window.TestPumpDispatcher();
                Assert.Equal(ActionPresets.Minecraft, window.TestSettings.ActiveProfileName);
                Assert.NotNull(window.SmokeProfileCardBorderBrush);
            });

            var saved = Ctx.ReadPersistedSettings();
            Assert.Equal(ActionPresets.Minecraft, saved.ActiveProfileName);
        }
        finally
        {
            Ctx.CloseAll();
        }
    }

    [Fact]
    public void Profile_combo_selection_persists_active_profile()
    {
        var window = Ctx.CreateWindow();
        try
        {
            WpfTestHost.Invoke(() =>
            {
                window.TestSelectTab(1);
                window.TestProfileCombo.SelectedItem = ActionPresets.Pedal;
                window.TestPumpDispatcher();
            });

            var saved = Ctx.ReadPersistedSettings();
            Assert.Equal(ActionPresets.Pedal, saved.ActiveProfileName);
        }
        finally
        {
            Ctx.CloseAll();
        }
    }
}
