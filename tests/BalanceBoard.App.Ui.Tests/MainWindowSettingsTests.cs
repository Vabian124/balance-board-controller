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
    public void Start_minimized_setting_minimizes_window_on_launch()
    {
        var window = Ctx.CreateWindow(seedSettings: new AppSettings { StartMinimized = true });
        try
        {
            WpfTestHost.Invoke(() => Assert.Equal(System.Windows.WindowState.Minimized, window.WindowState));
        }
        finally
        {
            Ctx.CloseAll();
        }
    }

    [Fact]
    public void Start_minimized_toggle_persists_to_settings_file()
    {
        var window = Ctx.CreateWindow(seedSettings: new AppSettings { StartMinimized = false });
        try
        {
            WpfTestHost.Invoke(() =>
            {
                window.TestSelectTab(1);
                window.TestStartMinimizedCheck.IsChecked = true;
                window.TestPumpDispatcher();
            });

            var saved = Ctx.ReadPersistedSettings();
            Assert.True(saved.StartMinimized);
        }
        finally
        {
            Ctx.CloseAll();
        }
    }

    [Fact]
    public void Poll_interval_slider_persists_and_applies_to_session()
    {
        var window = Ctx.CreateWindow(seedSettings: new AppSettings
        {
            UiDetailLevel = UiDetailLevel.Advanced,
            PollIntervalMs = 50,
        });

        try
        {
            WpfTestHost.Invoke(() =>
            {
                window.TestSelectTab(3);
                window.TestPollIntervalSlider.Value = 20;
                window.TestPumpDispatcher();
            });

            var saved = Ctx.ReadPersistedSettings();
            Assert.Equal(20, saved.PollIntervalMs);
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

    [Fact]
    public void Virtual_controller_backend_selection_persists_placeholder_mode()
    {
        var window = Ctx.CreateWindow(seedSettings: new AppSettings
        {
            UiDetailLevel = UiDetailLevel.Advanced,
            OutputMode = OutputMode.Keyboard,
        });

        try
        {
            WpfTestHost.Invoke(() =>
            {
                window.TestSelectTab(3);
                window.TestOutputModeCombo.SelectedIndex = 1;
                window.TestVirtualControllerBackendCombo.SelectedIndex = 1;
                window.TestPumpDispatcher();
            });

            var saved = Ctx.ReadPersistedSettings();
            Assert.Equal(OutputMode.VirtualController, saved.OutputMode);
            Assert.Equal(VirtualControllerBackend.Xbox360, saved.VirtualControllerBackend);
            Assert.False(saved.EnableVJoy);
            Assert.True(saved.DisableKeyboardActions);
        }
        finally
        {
            Ctx.CloseAll();
        }
    }

    [Fact]
    public void Virtual_controller_backend_panels_follow_selected_backend()
    {
        var window = Ctx.CreateWindow(seedSettings: new AppSettings
        {
            UiDetailLevel = UiDetailLevel.Advanced,
            VirtualControllerBackend = VirtualControllerBackend.VJoy,
            OutputMode = OutputMode.VirtualController,
        });

        try
        {
            WpfTestHost.Invoke(() =>
            {
                window.TestSelectTab(3);
                Assert.Equal(System.Windows.Visibility.Visible, window.TestVirtualControllerBackendRowVisibility);
                Assert.Equal(System.Windows.Visibility.Collapsed, window.TestVirtualControllerUnavailablePanelVisibility);
                Assert.Equal(System.Windows.Visibility.Visible, window.TestVJoySettingsPanelVisibility);

                window.TestVirtualControllerBackendCombo.SelectedIndex = 1;
                window.TestPumpDispatcher();

                Assert.Equal(System.Windows.Visibility.Visible, window.TestVirtualControllerBackendRowVisibility);
                Assert.Equal(System.Windows.Visibility.Visible, window.TestVirtualControllerUnavailablePanelVisibility);
                Assert.Equal(System.Windows.Visibility.Collapsed, window.TestVJoySettingsPanelVisibility);
            });
        }
        finally
        {
            Ctx.CloseAll();
        }
    }
}
