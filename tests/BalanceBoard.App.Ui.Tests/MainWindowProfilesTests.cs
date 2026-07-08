using System.Linq;
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
    public void Custom_profile_save_then_load_round_trips_and_persists()
    {
        var window = Ctx.CreateWindow(seedSettings: new AppSettings { DeadzonePercent = 5 });
        try
        {
            WpfTestHost.Invoke(() =>
            {
                window.TestSelectTab(1);
                window.TestSaveCustomProfile("Test A");
                window.TestPumpDispatcher();

                Assert.Contains("Test A", window.TestCustomProfileCombo.ItemsSource.Cast<string>());

                // Change a persisted setting, then load the profile back.
                window.TestDeadzoneSlider.Value = 15;
                window.TestPumpDispatcher();
                Assert.Equal(15, window.TestSettings.DeadzonePercent);

                Assert.True(window.TestLoadCustomProfile("Test A"));
                window.TestPumpDispatcher();
                Assert.Equal(5, window.TestSettings.DeadzonePercent);
            });

            var saved = Ctx.ReadPersistedSettings();
            Assert.Equal(5, saved.DeadzonePercent);
        }
        finally
        {
            Ctx.CloseAll();
        }
    }

    [Fact]
    public void Loading_custom_profile_preserves_connection_identity()
    {
        var window = Ctx.CreateWindow(seedSettings: new AppSettings
        {
            HasConnectedBefore = true,
            LastConnectedDeviceId = "0001A2B3C4D5",
        });
        try
        {
            WpfTestHost.Invoke(() =>
            {
                window.TestSelectTab(1);
                window.TestSaveCustomProfile("Conn");
                window.TestPumpDispatcher();
                Assert.True(window.TestLoadCustomProfile("Conn"));
                window.TestPumpDispatcher();

                // Profile files never carry connection identity, but loading must not wipe the live state.
                Assert.True(window.TestSettings.HasConnectedBefore);
                Assert.Equal("0001A2B3C4D5", window.TestSettings.LastConnectedDeviceId);
            });
        }
        finally
        {
            Ctx.CloseAll();
        }
    }

    [Fact]
    public void Reset_defaults_restores_defaults_but_keeps_connection_identity()
    {
        var window = Ctx.CreateWindow(seedSettings: new AppSettings
        {
            UiDetailLevel = UiDetailLevel.Advanced,
            UseSimpleSensitivity = false,
            DeadzonePercent = 18,
            HasConnectedBefore = true,
            LastConnectedDeviceId = "0001A2B3C4D5",
        });
        try
        {
            WpfTestHost.Invoke(() =>
            {
                window.TestSelectTab(1);
                window.TestResetDefaults();
                window.TestPumpDispatcher();

                Assert.Equal(new AppSettings().DeadzonePercent, window.TestSettings.DeadzonePercent);
                Assert.True(window.TestSettings.HasConnectedBefore);
                Assert.Equal("0001A2B3C4D5", window.TestSettings.LastConnectedDeviceId);
            });

            var saved = Ctx.ReadPersistedSettings();
            Assert.Equal(new AppSettings().DeadzonePercent, saved.DeadzonePercent);
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
