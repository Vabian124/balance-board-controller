using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BalanceBoard.Core.Models;
using BalanceBoard.Core.Services;

namespace BalanceBoard.App;

public partial class MainWindow : Window
{
    private readonly SettingsStore _settingsStore = new();
    private readonly BalanceBoardSession _session = new();
    private readonly FileLogService _fileLog = new();
    private AppSettings _settings = new();
    private bool _uiReady;
    private bool _shutdownCompleted;
    private string _lastHealthReport = string.Empty;

    public MainWindow(int competingProcessesStopped = 0)
    {
        _settings = _settingsStore.Load();
        _session.LoadSettings(_settings);
        InitializeComponent();
        HookSession();
        HookFileLog();
        PopulateUi();
        RefreshVJoyStatus();
        UpdateSliderLabels();
        _uiReady = true;

        if (competingProcessesStopped > 0)
        {
            Log($"Stopped {competingProcessesStopped} competing feeder process(es) before startup.");
            foreach (var name in FeederProcessCleanup.LastTerminatedProcesses)
            {
                Log($"  - {name}");
            }
        }

        Log("Balance Board Controller ready.");
        Log($"Session log file: {_fileLog.CurrentLogPath}");
        UpdateConnectionPill(false);
    }

    private void HookSession()
    {
        _session.Processed += OnProcessed;
        _session.Log += Log;
        _session.StatusChanged += status => Dispatcher.Invoke(() =>
        {
            StatusText.Text = status;
            UpdateConnectionPill(_session.IsConnected);
            Log(status);
        });
    }

    private void HookFileLog()
    {
        _fileLog.LineWritten += line => Dispatcher.Invoke(() =>
        {
            LogBox.AppendText(line + Environment.NewLine);
            LogBox.ScrollToEnd();
        });
    }

    private void PopulateUi()
    {
        _suppressSettingEvents = true;
        ProfileCombo.ItemsSource = ActionPresets.All;
        ProfileCombo.SelectedItem = ActionPresets.All.Contains(_settings.ActiveProfileName)
            ? _settings.ActiveProfileName
            : ActionPresets.Default;

        EnableVJoyCheck.IsChecked = _settings.EnableVJoy;
        SendCgCheck.IsChecked = _settings.SendCenterOfGravityToAxes;
        SendSensorsCheck.IsChecked = _settings.SendLoadSensorsToAxes;
        DisableActionsCheck.IsChecked = _settings.DisableKeyboardActions;
        AutoConnectCheck.IsChecked = _settings.AutoConnectOnStartup;
        AutoTareCheck.IsChecked = _settings.AutoTareOnConnect;
        TriggerLeftRightSlider.Value = _settings.TriggerLeftRight;
        TriggerForwardBackwardSlider.Value = _settings.TriggerForwardBackward;
        DeadzoneSlider.Value = _settings.DeadzonePercent;
        SensitivitySlider.Value = _settings.Sensitivity;
        InvertXCheck.IsChecked = _settings.InvertX;
        InvertYCheck.IsChecked = _settings.InvertY;

        VJoyDeviceCombo.Items.Clear();
        for (uint i = 1; i <= 16; i++) VJoyDeviceCombo.Items.Add(i);
        VJoyDeviceCombo.SelectedItem = _settings.VJoyDeviceId;
        _suppressSettingEvents = false;
        UpdateActiveActionsSummary();
    }

    private void UpdateSliderLabels()
    {
        DeadzoneLabel.Text = $"Deadzone: {DeadzoneSlider.Value:0}%";
        SensitivityLabel.Text = $"Sensitivity: {SensitivitySlider.Value:0.0}x";
        TriggerLeftRightLabel.Text = $"Left/right trigger: {TriggerLeftRightSlider.Value:0}%";
        TriggerForwardBackwardLabel.Text = $"Forward/back trigger: {TriggerForwardBackwardSlider.Value:0}%";
    }

    private void UpdateActiveActionsSummary()
    {
        if (!_uiReady) return;

        ActiveActionsText.Text = _settings.DisableKeyboardActions
            ? $"Profile: {_settings.ActiveProfileName} · vJoy output"
            : $"Profile: {_settings.ActiveProfileName} · {DescribeBindings()}";
    }

    private string DescribeBindings()
    {
        var parts = new List<string>();
        foreach (var pair in _settings.Actions.OrderBy(p => p.Key))
        {
            var label = DescribeBinding(pair.Value);
            if (label is not null)
            {
                parts.Add($"{pair.Key}: {label}");
            }
        }

        return parts.Count == 0 ? "No keyboard/mouse bindings" : string.Join(" · ", parts);
    }

    private static string? DescribeBinding(ActionBinding binding) =>
        binding.Kind switch
        {
            ActionKind.Key => binding.KeyName,
            ActionKind.MouseButton => binding.MouseButton,
            ActionKind.MouseMoveX => $"mouse X {binding.Amount}",
            ActionKind.MouseMoveY => $"mouse Y {binding.Amount}",
            _ => null,
        };

    private void OnProcessed(ProcessedBalance data)
    {
        Dispatcher.Invoke(() =>
        {
            BoardVisual.Data = data;
            WeightText.Text = $"Weight: {data.WeightKg:0.0} kg";
            BalanceText.Text = $"Balance X/Y: {data.BalanceX:0.0}% / {data.BalanceY:0.0}%";
            DirectionText.Text = DescribeDirection(data);
            ActiveActionsText.Text = DescribeActiveInputs(data);
            SensorText.Text =
                $"TL {data.TopLeftKg:0.0}  TR {data.TopRightKg:0.0}  BL {data.BottomLeftKg:0.0}  BR {data.BottomRightKg:0.0}\n" +
                $"vJoy axes  X={data.JoyX,6}  Y={data.JoyY,6}  Z={data.JoyZ,6}";
        });
    }

    private static string DescribeDirection(ProcessedBalance data)
    {
        if (data.WeightKg < 5)
        {
            return "Step on the board to begin";
        }

        var parts = new List<string>();
        if (data.MoveForward) parts.Add("forward");
        if (data.MoveBackward) parts.Add("backward");
        if (data.MoveLeft) parts.Add("left");
        if (data.MoveRight) parts.Add("right");
        if (data.Jump) parts.Add("jump");
        if (parts.Count == 0) return "Centered";
        return "Leaning " + string.Join(" · ", parts);
    }

    private string DescribeActiveInputs(ProcessedBalance data)
    {
        if (data.WeightKg < 5)
        {
            return $"Profile: {_settings.ActiveProfileName}";
        }

        var active = new List<string>();
        if (data.MoveForward) active.Add("Forward");
        if (data.MoveBackward) active.Add("Backward");
        if (data.MoveLeft) active.Add("Left");
        if (data.MoveRight) active.Add("Right");
        if (data.Modifier) active.Add("Modifier");
        if (data.Jump) active.Add("Jump");
        if (data.DiagonalLeft) active.Add("Diagonal left");
        if (data.DiagonalRight) active.Add("Diagonal right");

        return active.Count == 0
            ? $"Profile: {_settings.ActiveProfileName} · Centered"
            : $"Active: {string.Join(", ", active)}";
    }

    private void RefreshVJoyStatus()
    {
        var diag = VJoyDiagnostics.Inspect(_settings.VJoyDeviceId);
        VJoyStatusText.Text =
            $"Driver: {(diag.DriverEnabled ? "OK" : "MISSING")}\n" +
            $"Device status: {diag.DeviceStatus}\n" +
            $"Axes X/Y/Z/RX/RY/RZ: {diag.HasAxisX}/{diag.HasAxisY}/{diag.HasAxisZ}/{diag.HasAxisRx}/{diag.HasAxisRy}/{diag.HasAxisRz}\n" +
            $"Buttons: {diag.ButtonCount}\n" +
            $"DLL match: {diag.DriverMatchesDll}\n" +
            (diag.Error ?? "No issues detected.");

        var ok = diag.DriverEnabled && diag.DeviceStatus is not "VJD_STAT_MISS" and not "VJD_STAT_BUSY";
        VJoyPillText.Text = ok ? "vJoy: ready" : "vJoy: needs attention";
        VJoyPill.Background = new SolidColorBrush(ok
            ? (Color)FindResource("SurfaceAltColor")
            : (Color)FindResource("WarningColor"));
    }

    private void UpdateConnectionPill(bool connected)
    {
        ConnectionPillText.Text = connected ? "Connected" : "Disconnected";
        ConnectionPill.Background = new SolidColorBrush(connected
            ? (Color)FindResource("SuccessColor")
            : (Color)FindResource("SurfaceAltColor"));
    }

    private void Log(string message)
    {
        _fileLog.Write(message);
    }

    private bool _suppressSettingEvents;

    private void SaveSettingsFromUi()
    {
        if (!_uiReady || _suppressSettingEvents) return;

        _settings.EnableVJoy = EnableVJoyCheck.IsChecked == true;
        _settings.SendCenterOfGravityToAxes = SendCgCheck.IsChecked == true;
        _settings.SendLoadSensorsToAxes = SendSensorsCheck.IsChecked == true;
        _settings.DisableKeyboardActions = DisableActionsCheck.IsChecked == true;
        _settings.AutoConnectOnStartup = AutoConnectCheck.IsChecked == true;
        _settings.AutoTareOnConnect = AutoTareCheck.IsChecked == true;
        _settings.TriggerLeftRight = (int)TriggerLeftRightSlider.Value;
        _settings.TriggerForwardBackward = (int)TriggerForwardBackwardSlider.Value;
        _settings.DeadzonePercent = DeadzoneSlider.Value;
        _settings.Sensitivity = SensitivitySlider.Value;
        _settings.InvertX = InvertXCheck.IsChecked == true;
        _settings.InvertY = InvertYCheck.IsChecked == true;
        if (VJoyDeviceCombo.SelectedItem is uint id) _settings.VJoyDeviceId = id;
        else if (VJoyDeviceCombo.SelectedItem is int intId) _settings.VJoyDeviceId = (uint)intId;

        UpdateSliderLabels();
        UpdateActiveActionsSummary();
        _settingsStore.Save(_settings);
        _session.LoadSettings(_settings);
        RefreshVJoyStatus();
    }

    private void SettingChanged(object sender, RoutedEventArgs e)
    {
        SaveSettingsFromUi();
    }

    private void SettingChanged(object sender, SelectionChangedEventArgs e) => SaveSettingsFromUi();

    private void SettingChanged(object sender, TextChangedEventArgs e) => SaveSettingsFromUi();

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (!_settings.AutoConnectOnStartup || _session.IsConnected)
        {
            return;
        }

        Log("Auto-connect enabled — connecting to balance board...");
        if (!_session.Connect())
        {
            Log("Auto-connect failed. Pair the board in Windows Bluetooth settings, then click Connect.");
        }
    }

    private void ProfileCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_uiReady || _suppressSettingEvents || ProfileCombo.SelectedItem is not string profile)
        {
            return;
        }

        _session.ApplyProfile(profile);
        SyncUiFromSettings();
        SaveSettingsFromUi();
    }

    private void SyncUiFromSettings()
    {
        _suppressSettingEvents = true;
        EnableVJoyCheck.IsChecked = _settings.EnableVJoy;
        SendCgCheck.IsChecked = _settings.SendCenterOfGravityToAxes;
        SendSensorsCheck.IsChecked = _settings.SendLoadSensorsToAxes;
        DisableActionsCheck.IsChecked = _settings.DisableKeyboardActions;
        TriggerLeftRightSlider.Value = _settings.TriggerLeftRight;
        TriggerForwardBackwardSlider.Value = _settings.TriggerForwardBackward;
        if (ActionPresets.All.Contains(_settings.ActiveProfileName))
        {
            ProfileCombo.SelectedItem = _settings.ActiveProfileName;
        }

        _suppressSettingEvents = false;
        UpdateSliderLabels();
        UpdateActiveActionsSummary();
    }

    private void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        SaveSettingsFromUi();
        if (!_session.Connect())
        {
            MessageBox.Show(this,
                "Could not connect. Pair the board in Windows Bluetooth settings, then hold SYNC while connecting.",
                "Connection failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void DisconnectButton_Click(object sender, RoutedEventArgs e) => _session.Disconnect();

    private void SetupButton_Click(object sender, RoutedEventArgs e)
    {
        var wizard = new SetupWizardWindow(_session) { Owner = this };
        if (wizard.ShowDialog() == true)
        {
            _session.ApplyControllerPreset();
            SyncUiFromSettings();
            SaveSettingsFromUi();
        }
    }

    private void Tare_Click(object sender, RoutedEventArgs e) => _session.Tare();
    private void SetCenter_Click(object sender, RoutedEventArgs e) => _session.SetCenter();
    private void ResetCenter_Click(object sender, RoutedEventArgs e) => _session.ResetCenter();

    private void GamePreset_Click(object sender, RoutedEventArgs e)
    {
        _session.ApplyControllerPreset();
        SyncUiFromSettings();
        SaveSettingsFromUi();
    }

    private void PedalPreset_Click(object sender, RoutedEventArgs e)
    {
        _session.ApplyPedalPreset();
        SyncUiFromSettings();
        SaveSettingsFromUi();
    }

    private void KeyboardPreset_Click(object sender, RoutedEventArgs e)
    {
        _session.ApplyKeyboardPreset();
        SyncUiFromSettings();
        SaveSettingsFromUi();
    }

    private void RunHealthCheckButton_Click(object sender, RoutedEventArgs e)
    {
        Log("Running health check...");
        var report = DiagnosticsReport.Run(_settings.VJoyDeviceId);
        _lastHealthReport = report.ToClipboardText();

        HealthSummaryText.Text = report.Summary;
        HealthSummaryText.Foreground = new SolidColorBrush(report.IsHealthy
            ? (Color)FindResource("SuccessColor")
            : (Color)FindResource("WarningColor"));

        foreach (var line in report.Lines)
        {
            Log(line);
        }

        RefreshVJoyStatus();
    }

    private void CopyReportButton_Click(object sender, RoutedEventArgs e)
    {
        var text = string.IsNullOrWhiteSpace(_lastHealthReport) ? LogBox.Text : _lastHealthReport;
        if (string.IsNullOrWhiteSpace(text))
        {
            MessageBox.Show(this, "Run a health check first, or wait for log output.", "Nothing to copy", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        Clipboard.SetText(text);
        Log("Diagnostics copied to clipboard.");
    }

    private void OpenLogFolderButton_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(_fileLog.LogDirectory);
        Process.Start(new ProcessStartInfo(_fileLog.LogDirectory) { UseShellExecute = true });
        Log($"Opened log folder: {_fileLog.LogDirectory}");
    }

    private void ClearLogViewButton_Click(object sender, RoutedEventArgs e)
    {
        LogBox.Clear();
        Log("Log view cleared (file log preserved).");
    }

    public void ForceShutdown()
    {
        if (_shutdownCompleted) return;
        _shutdownCompleted = true;
        if (_uiReady) SaveSettingsFromUi();
        _session.Dispose();
        Log("Application shutting down.");
    }

    protected override void OnClosed(EventArgs e)
    {
        ForceShutdown();
        base.OnClosed(e);
    }
}
