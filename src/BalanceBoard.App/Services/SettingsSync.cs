using System.Windows;
using System.Windows.Controls;
using BalanceBoard.Core.Models;
using BalanceBoard.Core.Services;

namespace BalanceBoard.App.Services;

/// <summary>
/// Single map between <see cref="AppSettings"/> and MainWindow setting controls.
/// </summary>
public sealed class SettingsSync
{
    private readonly AppSettings _settings;
    private readonly SettingsControls _ui;

    public SettingsSync(AppSettings settings, SettingsControls ui)
    {
        _settings = settings;
        _ui = ui;
    }

    public void InitializeComboItems()
    {
        _ui.ProfileCombo.ItemsSource = ActionPresets.All;
        _ui.DetailLevelCombo.Items.Clear();
        _ui.DetailLevelCombo.Items.Add("Simple");
        _ui.DetailLevelCombo.Items.Add("Standard");
        _ui.DetailLevelCombo.Items.Add("Advanced");
        _ui.ThemeCombo.ItemsSource = new[] { ThemePreference.System, ThemePreference.Light, ThemePreference.Dark };
        PopulateOutputModeCombo();
        PopulateJumpVJoyButtonCombo();
        EnsureResponseCurveComboItems();
    }

    public void ApplyToUi()
    {
        _ui.ProfileCombo.SelectedItem = ActionPresets.All.Contains(_settings.ActiveProfileName)
            ? _settings.ActiveProfileName
            : ActionPresets.Default;
        _ui.DetailLevelCombo.SelectedIndex = (int)_settings.UiDetailLevel;
        _ui.ThemeCombo.SelectedItem = _settings.ThemePreference;

        _ui.SendCgCheck.IsChecked = _settings.SendCenterOfGravityToAxes;
        _ui.SendSensorsCheck.IsChecked = _settings.SendLoadSensorsToAxes;
        _ui.MapJumpVJoyCheck.IsChecked = _settings.MapJumpToVJoyButton;
        PopulateOutputModeCombo();
        PopulateJumpVJoyButtonCombo();
        _ui.JumpVJoyButtonCombo.SelectedItem = Math.Clamp(_settings.JumpVJoyButton, 1, 32);
        UpdateJumpVJoyPanelVisibility();

        _ui.AutoConnectCheck.IsChecked = _settings.AutoConnectOnStartup;
        _ui.StartMinimizedCheck.IsChecked = _settings.StartMinimized;
        _ui.AutoTareCheck.IsChecked = _settings.AutoTareOnConnect;
        _ui.PollIntervalSlider.Value = _settings.PollIntervalMs;
        _ui.TriggerLeftRightSlider.Value = _settings.TriggerLeftRight;
        _ui.TriggerForwardBackwardSlider.Value = _settings.TriggerForwardBackward;
        _ui.DeadzoneSlider.Value = _settings.DeadzonePercent;
        _ui.SensitivitySlider.Value = _settings.Sensitivity;
        _ui.JumpThresholdSlider.Value = _settings.JumpWeightThresholdKg;
        _ui.JumpHoldSlider.Value = _settings.JumpHoldSeconds;
        _ui.SimpleSensitivityCheck.IsChecked = _settings.UseSimpleSensitivity;
        _ui.OneFootModeCheck.IsChecked = _settings.OneFootMode;
        _ui.InvertXCheck.IsChecked = _settings.InvertX;
        _ui.InvertYCheck.IsChecked = _settings.InvertY;
        _ui.LockLeftRightAxisCheck.IsChecked = _settings.LockLeftRightAxis;
        _ui.LockForwardBackwardAxisCheck.IsChecked = _settings.LockForwardBackwardAxis;

        var splitSens = _settings.SensitivityLeftRight is not null
            || _settings.SensitivityForwardBackward is not null;
        _ui.SplitAxisSensitivityCheck.IsChecked = splitSens;
        _ui.SensitivityLeftRightSlider.Value = _settings.SensitivityLeftRight ?? 0;
        _ui.SensitivityForwardBackwardSlider.Value = _settings.SensitivityForwardBackward ?? 0;

        var splitDz = _settings.DeadzoneLeftRightPercent is not null
            || _settings.DeadzoneForwardBackwardPercent is not null;
        _ui.SplitAxisDeadzoneCheck.IsChecked = splitDz;
        _ui.DeadzoneLeftRightSlider.Value = _settings.DeadzoneLeftRightPercent ?? _settings.DeadzonePercent;
        _ui.DeadzoneForwardBackwardSlider.Value = _settings.DeadzoneForwardBackwardPercent ?? _settings.DeadzonePercent;

        EnsureResponseCurveComboItems();
        _ui.ResponseCurveCombo.SelectedIndex = (int)_settings.ResponseCurve;
        _ui.SessionLogExpander.IsExpanded = _settings.SessionLogExpanded;
    }

    public void ReadFromUi()
    {
        if (_ui.OutputModeCombo.SelectedIndex == 1)
        {
            _settings.SetOutputMode(OutputMode.VJoy);
        }
        else if (_ui.OutputModeCombo.SelectedIndex == 0)
        {
            _settings.SetOutputMode(OutputMode.Keyboard);
        }

        _ui.SendCgCheck.IsChecked = _settings.SendCenterOfGravityToAxes;
        _ui.SendSensorsCheck.IsChecked = _settings.SendLoadSensorsToAxes;

        _settings.MapJumpToVJoyButton = _ui.MapJumpVJoyCheck.IsChecked == true;
        if (_ui.JumpVJoyButtonCombo.SelectedItem is int jumpButton)
        {
            _settings.JumpVJoyButton = jumpButton;
        }

        _settings.AutoConnectOnStartup = _ui.AutoConnectCheck.IsChecked == true;
        _settings.StartMinimized = _ui.StartMinimizedCheck.IsChecked == true;
        _settings.AutoTareOnConnect = _ui.AutoTareCheck.IsChecked == true;
        _settings.PollIntervalMs = (int)_ui.PollIntervalSlider.Value;
        _settings.TriggerLeftRight = (int)_ui.TriggerLeftRightSlider.Value;
        _settings.TriggerForwardBackward = (int)_ui.TriggerForwardBackwardSlider.Value;
        _settings.DeadzonePercent = _ui.DeadzoneSlider.Value;
        _settings.Sensitivity = _ui.SensitivitySlider.Value;
        _settings.JumpWeightThresholdKg = (float)_ui.JumpThresholdSlider.Value;
        _settings.JumpHoldSeconds = _ui.JumpHoldSlider.Value;
        _settings.UseSimpleSensitivity = _ui.SimpleSensitivityCheck.IsChecked == true;
        _settings.OneFootMode = _ui.OneFootModeCheck.IsChecked == true;

        if (_ui.ResponseCurveCombo.SelectedIndex is >= 0 and <= (int)ResponseCurve.MinecraftSnappy)
        {
            _settings.ResponseCurve = (ResponseCurve)_ui.ResponseCurveCombo.SelectedIndex;
        }

        if (_ui.ProfileCombo.SelectedItem is string profile)
        {
            _settings.ActiveProfileName = profile;
        }

        if (_ui.ThemeCombo.SelectedItem is ThemePreference theme)
        {
            _settings.ThemePreference = theme;
        }

        if (_ui.DetailLevelCombo.SelectedIndex is >= 0 and <= 2)
        {
            _settings.UiDetailLevel = (UiDetailLevel)_ui.DetailLevelCombo.SelectedIndex;
        }

        _settings.SessionLogExpanded = _ui.SessionLogExpander.IsExpanded;

        _settings.InvertX = _ui.InvertXCheck.IsChecked == true;
        _settings.InvertY = _ui.InvertYCheck.IsChecked == true;
        _settings.LockLeftRightAxis = _ui.LockLeftRightAxisCheck.IsChecked == true;
        _settings.LockForwardBackwardAxis = _ui.LockForwardBackwardAxisCheck.IsChecked == true;
        _settings.SensitivityLeftRight = _ui.SplitAxisSensitivityCheck.IsChecked == true
            ? _ui.SensitivityLeftRightSlider.Value
            : null;
        _settings.SensitivityForwardBackward = _ui.SplitAxisSensitivityCheck.IsChecked == true
            ? _ui.SensitivityForwardBackwardSlider.Value
            : null;
        _settings.DeadzoneLeftRightPercent = _ui.SplitAxisDeadzoneCheck.IsChecked == true
            ? _ui.DeadzoneLeftRightSlider.Value
            : null;
        _settings.DeadzoneForwardBackwardPercent = _ui.SplitAxisDeadzoneCheck.IsChecked == true
            ? _ui.DeadzoneForwardBackwardSlider.Value
            : null;

        if (_ui.VJoyDeviceCombo.SelectedItem is VJoyDeviceInfo deviceInfo)
        {
            _settings.VJoyDeviceId = deviceInfo.DeviceId;
        }
        else if (_ui.VJoyDeviceCombo.SelectedItem is uint id)
        {
            _settings.VJoyDeviceId = id;
        }
        else if (_ui.VJoyDeviceCombo.SelectedItem is int intId)
        {
            _settings.VJoyDeviceId = (uint)intId;
        }

        UpdateJumpVJoyPanelVisibility();
    }

    public void PopulateOutputModeCombo()
    {
        _ui.OutputModeCombo.ItemsSource = new[]
        {
            "Keyboard & mouse (WASD, Space, etc.)",
            "Virtual controller (vJoy)",
        };
        _ui.OutputModeCombo.SelectedIndex = _settings.OutputMode == OutputMode.VJoy ? 1 : 0;
    }

    public void UpdateJumpVJoyPanelVisibility() =>
        _ui.JumpVJoyPanel.Visibility = _ui.MapJumpVJoyCheck.IsChecked == true && _settings.OutputMode == OutputMode.VJoy
            ? Visibility.Visible
            : Visibility.Collapsed;

    private void PopulateJumpVJoyButtonCombo()
    {
        if (_ui.JumpVJoyButtonCombo.ItemsSource is null)
        {
            _ui.JumpVJoyButtonCombo.ItemsSource = Enumerable.Range(1, 32).ToList();
        }
    }

    private void EnsureResponseCurveComboItems()
    {
        if (_ui.ResponseCurveCombo.Items.Count > 0)
        {
            return;
        }

        foreach (ResponseCurve curve in Enum.GetValues<ResponseCurve>())
        {
            _ui.ResponseCurveCombo.Items.Add(SensitivityCurve.DisplayName(curve));
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
