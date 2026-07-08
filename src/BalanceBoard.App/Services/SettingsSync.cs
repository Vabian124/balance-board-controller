using System.Windows;
using System.Windows.Controls;
using BalanceBoard.Core.Models;
using BalanceBoard.Core.Services;

namespace BalanceBoard.App.Services;

/// <summary>
/// Single map between <see cref="AppSettings"/> and MainWindow setting controls.
/// </summary>
public sealed class SettingsSync(AppSettings settings, SettingsControls ui)
{

    public void InitializeComboItems()
    {
        ui.ProfileCombo.ItemsSource = ActionPresets.All;
        ui.DetailLevelCombo.Items.Clear();
        ui.DetailLevelCombo.Items.Add("Simple");
        ui.DetailLevelCombo.Items.Add("Standard");
        ui.DetailLevelCombo.Items.Add("Advanced");
        ui.ThemeCombo.ItemsSource = new[] { ThemePreference.System, ThemePreference.Light, ThemePreference.Dark };
        PopulateOutputModeCombo();
        PopulateJumpVJoyButtonCombo();
        EnsureResponseCurveComboItems();
    }

    public void ApplyToUi()
    {
        ui.ProfileCombo.SelectedItem = ActionPresets.All.Contains(settings.ActiveProfileName)
            ? settings.ActiveProfileName
            : ActionPresets.Default;
        ui.DetailLevelCombo.SelectedIndex = (int)settings.UiDetailLevel;
        ui.ThemeCombo.SelectedItem = settings.ThemePreference;

        ui.SendCgCheck.IsChecked = settings.SendCenterOfGravityToAxes;
        ui.SendSensorsCheck.IsChecked = settings.SendLoadSensorsToAxes;
        ui.MapJumpVJoyCheck.IsChecked = settings.MapJumpToVJoyButton;
        PopulateOutputModeCombo();
        PopulateJumpVJoyButtonCombo();
        ui.JumpVJoyButtonCombo.SelectedItem = Math.Clamp(settings.JumpVJoyButton, 1, 32);
        UpdateJumpVJoyPanelVisibility();

        ui.AutoConnectCheck.IsChecked = settings.AutoConnectOnStartup;
        ui.StartMinimizedCheck.IsChecked = settings.StartMinimized;
        ui.AutoTareCheck.IsChecked = settings.AutoTareOnConnect;
        ui.PollIntervalSlider.Value = settings.PollIntervalMs;
        ui.TriggerLeftRightSlider.Value = settings.TriggerLeftRight;
        ui.TriggerForwardBackwardSlider.Value = settings.TriggerForwardBackward;
        ui.DeadzoneSlider.Value = settings.DeadzonePercent;
        ui.SensitivitySlider.Value = settings.Sensitivity;
        ui.JumpThresholdSlider.Value = settings.JumpWeightThresholdKg;
        ui.JumpHoldSlider.Value = settings.JumpHoldSeconds;
        ui.SimpleSensitivityCheck.IsChecked = settings.UseSimpleSensitivity;
        ui.OneFootModeCheck.IsChecked = settings.OneFootMode;
        ui.InvertXCheck.IsChecked = settings.InvertX;
        ui.InvertYCheck.IsChecked = settings.InvertY;
        ui.LockLeftRightAxisCheck.IsChecked = settings.LockLeftRightAxis;
        ui.LockForwardBackwardAxisCheck.IsChecked = settings.LockForwardBackwardAxis;

        var splitSens = settings.SensitivityLeftRight is not null
            || settings.SensitivityForwardBackward is not null;
        ui.SplitAxisSensitivityCheck.IsChecked = splitSens;
        ui.SensitivityLeftRightSlider.Value = settings.SensitivityLeftRight ?? 0;
        ui.SensitivityForwardBackwardSlider.Value = settings.SensitivityForwardBackward ?? 0;

        var splitDz = settings.DeadzoneLeftRightPercent is not null
            || settings.DeadzoneForwardBackwardPercent is not null;
        ui.SplitAxisDeadzoneCheck.IsChecked = splitDz;
        ui.DeadzoneLeftRightSlider.Value = settings.DeadzoneLeftRightPercent ?? settings.DeadzonePercent;
        ui.DeadzoneForwardBackwardSlider.Value = settings.DeadzoneForwardBackwardPercent ?? settings.DeadzonePercent;

        EnsureResponseCurveComboItems();
        ui.ResponseCurveCombo.SelectedIndex = (int)settings.ResponseCurve;
        ui.SessionLogExpander.IsExpanded = settings.SessionLogExpanded;
    }

    public void ReadFromUi()
    {
        if (ui.OutputModeCombo.SelectedIndex == 1)
        {
            settings.SetOutputMode(OutputMode.VJoy);
        }
        else if (ui.OutputModeCombo.SelectedIndex == 0)
        {
            settings.SetOutputMode(OutputMode.Keyboard);
        }

        ui.SendCgCheck.IsChecked = settings.SendCenterOfGravityToAxes;
        ui.SendSensorsCheck.IsChecked = settings.SendLoadSensorsToAxes;

        settings.MapJumpToVJoyButton = ui.MapJumpVJoyCheck.IsChecked == true;
        if (ui.JumpVJoyButtonCombo.SelectedItem is int jumpButton)
        {
            settings.JumpVJoyButton = jumpButton;
        }

        settings.AutoConnectOnStartup = ui.AutoConnectCheck.IsChecked == true;
        settings.StartMinimized = ui.StartMinimizedCheck.IsChecked == true;
        settings.AutoTareOnConnect = ui.AutoTareCheck.IsChecked == true;
        settings.PollIntervalMs = (int)ui.PollIntervalSlider.Value;
        settings.TriggerLeftRight = (int)ui.TriggerLeftRightSlider.Value;
        settings.TriggerForwardBackward = (int)ui.TriggerForwardBackwardSlider.Value;
        settings.DeadzonePercent = ui.DeadzoneSlider.Value;
        settings.Sensitivity = ui.SensitivitySlider.Value;
        settings.JumpWeightThresholdKg = (float)ui.JumpThresholdSlider.Value;
        settings.JumpHoldSeconds = ui.JumpHoldSlider.Value;
        settings.UseSimpleSensitivity = ui.SimpleSensitivityCheck.IsChecked == true;
        settings.OneFootMode = ui.OneFootModeCheck.IsChecked == true;

        if (ui.ResponseCurveCombo.SelectedIndex is >= 0 and <= (int)ResponseCurve.MinecraftSnappy)
        {
            settings.ResponseCurve = (ResponseCurve)ui.ResponseCurveCombo.SelectedIndex;
        }

        if (ui.ProfileCombo.SelectedItem is string profile)
        {
            settings.ActiveProfileName = profile;
        }

        if (ui.ThemeCombo.SelectedItem is ThemePreference theme)
        {
            settings.ThemePreference = theme;
        }

        if (ui.DetailLevelCombo.SelectedIndex is >= 0 and <= 2)
        {
            settings.UiDetailLevel = (UiDetailLevel)ui.DetailLevelCombo.SelectedIndex;
        }

        settings.SessionLogExpanded = ui.SessionLogExpander.IsExpanded;

        settings.InvertX = ui.InvertXCheck.IsChecked == true;
        settings.InvertY = ui.InvertYCheck.IsChecked == true;
        settings.LockLeftRightAxis = ui.LockLeftRightAxisCheck.IsChecked == true;
        settings.LockForwardBackwardAxis = ui.LockForwardBackwardAxisCheck.IsChecked == true;
        settings.SensitivityLeftRight = ui.SplitAxisSensitivityCheck.IsChecked == true
            ? ui.SensitivityLeftRightSlider.Value
            : null;
        settings.SensitivityForwardBackward = ui.SplitAxisSensitivityCheck.IsChecked == true
            ? ui.SensitivityForwardBackwardSlider.Value
            : null;
        settings.DeadzoneLeftRightPercent = ui.SplitAxisDeadzoneCheck.IsChecked == true
            ? ui.DeadzoneLeftRightSlider.Value
            : null;
        settings.DeadzoneForwardBackwardPercent = ui.SplitAxisDeadzoneCheck.IsChecked == true
            ? ui.DeadzoneForwardBackwardSlider.Value
            : null;

        if (ui.VJoyDeviceCombo.SelectedItem is VJoyDeviceInfo deviceInfo)
        {
            settings.VJoyDeviceId = deviceInfo.DeviceId;
        }
        else if (ui.VJoyDeviceCombo.SelectedItem is uint id)
        {
            settings.VJoyDeviceId = id;
        }
        else if (ui.VJoyDeviceCombo.SelectedItem is int intId)
        {
            settings.VJoyDeviceId = (uint)intId;
        }

        UpdateJumpVJoyPanelVisibility();
    }

    public void PopulateOutputModeCombo()
    {
        ui.OutputModeCombo.ItemsSource = new[]
        {
            "Keyboard & mouse (WASD, Space, etc.)",
            "Virtual controller (vJoy)",
        };
        ui.OutputModeCombo.SelectedIndex = settings.OutputMode == OutputMode.VJoy ? 1 : 0;
    }

    public void UpdateJumpVJoyPanelVisibility() =>
        ui.JumpVJoyPanel.Visibility = ui.MapJumpVJoyCheck.IsChecked == true && settings.OutputMode == OutputMode.VJoy
            ? Visibility.Visible
            : Visibility.Collapsed;

    private void PopulateJumpVJoyButtonCombo()
    {
        if (ui.JumpVJoyButtonCombo.ItemsSource is null)
        {
            ui.JumpVJoyButtonCombo.ItemsSource = Enumerable.Range(1, 32).ToList();
        }
    }

    private void EnsureResponseCurveComboItems()
    {
        if (ui.ResponseCurveCombo.Items.Count > 0)
        {
            return;
        }

        foreach (ResponseCurve curve in Enum.GetValues<ResponseCurve>())
        {
            ui.ResponseCurveCombo.Items.Add(SensitivityCurve.DisplayName(curve));
        }
    }
}

public sealed class SettingsControls
{
    public required ComboBox ProfileCombo { get; init; }
    public required ComboBox DetailLevelCombo { get; init; }
    public required ComboBox ThemeCombo { get; init; }
    public required ComboBox OutputModeCombo { get; init; }
    public required ComboBox JumpVJoyButtonCombo { get; init; }
    public required ComboBox VJoyDeviceCombo { get; init; }
    public required ComboBox ResponseCurveCombo { get; init; }
    public required CheckBox SendCgCheck { get; init; }
    public required CheckBox SendSensorsCheck { get; init; }
    public required CheckBox MapJumpVJoyCheck { get; init; }
    public required CheckBox AutoConnectCheck { get; init; }
    public required CheckBox StartMinimizedCheck { get; init; }
    public required CheckBox AutoTareCheck { get; init; }
    public required CheckBox SimpleSensitivityCheck { get; init; }
    public required CheckBox OneFootModeCheck { get; init; }
    public required CheckBox InvertXCheck { get; init; }
    public required CheckBox InvertYCheck { get; init; }
    public required CheckBox LockLeftRightAxisCheck { get; init; }
    public required CheckBox LockForwardBackwardAxisCheck { get; init; }
    public required CheckBox SplitAxisSensitivityCheck { get; init; }
    public required CheckBox SplitAxisDeadzoneCheck { get; init; }
    public required Slider PollIntervalSlider { get; init; }
    public required Slider TriggerLeftRightSlider { get; init; }
    public required Slider TriggerForwardBackwardSlider { get; init; }
    public required Slider DeadzoneSlider { get; init; }
    public required Slider SensitivitySlider { get; init; }
    public required Slider JumpThresholdSlider { get; init; }
    public required Slider JumpHoldSlider { get; init; }
    public required Slider SensitivityLeftRightSlider { get; init; }
    public required Slider SensitivityForwardBackwardSlider { get; init; }
    public required Slider DeadzoneLeftRightSlider { get; init; }
    public required Slider DeadzoneForwardBackwardSlider { get; init; }
    public required Expander SessionLogExpander { get; init; }
    public required StackPanel JumpVJoyPanel { get; init; }
}
