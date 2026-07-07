using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BalanceBoard.App.Services;
using BalanceBoard.Core.Models;
using BalanceBoard.Core.Services;

namespace BalanceBoard.App;

public partial class MainWindow : Window
{
    private readonly SettingsStore _settingsStore = new();
    private readonly BalanceBoardSession _session = new();
    private readonly FileLogService _fileLog = new();
    private readonly StartupOptions _startupOptions;
    private AppSettings _settings = new();
    private bool _uiReady;
    private bool _shutdownCompleted;
    private bool _connectInProgress;
    private CancellationTokenSource? _connectCts;
    private string _lastHealthReport = string.Empty;

    public MainWindow(StartupOptions startupOptions, int competingProcessesStopped = 0)
    {
        _startupOptions = startupOptions;
        _settings = _settingsStore.Load();
        _session.LoadSettings(_settings, initializeVJoy: false);
        InitializeComponent();
        _uiReady = true;
        HookSession();
        HookFileLog();
        PopulateUi();
        RefreshVJoyStatus();
        UpdateSliderLabels();
        UpdateConnectUi(isBusy: false);

        if (competingProcessesStopped > 0)
        {
            Log($"Stopped {competingProcessesStopped} competing process(es).");
        }

        Log("Ready.");
        Log($"Log file: {_fileLog.CurrentLogPath}");
        UpdateConnectionChip(false);
    }

    public void RunDeferredStartup(bool connectOnLaunch, int competingProcessesStopped = 0)
    {
        if (competingProcessesStopped > 0)
        {
            Log($"Startup cleanup stopped {competingProcessesStopped} competing process(es).");
        }

        BluetoothPairingService.Warmup();
        _session.LoadSettings(_settings, initializeVJoy: true);

        if (_settings.ActiveProfileName != ActionPresets.GameController)
        {
            _session.ApplyControllerPreset();
            SyncUiFromSettings();
        }

        var diag = VJoyDiagnostics.Inspect(_settings.VJoyDeviceId);
        Log($"vJoy: driver={(diag.DriverEnabled ? "OK" : "missing")}, status={diag.DeviceStatus}");
        RefreshVJoyStatus();

        if (connectOnLaunch)
        {
            Log("Launch flag --connect: starting full pairing flow.");
            BeginConnect(ConnectionIntent.PairAndConnect);
            return;
        }

        if (!_settings.HasConnectedBefore)
        {
            StatusText.Text = "Welcome — click Connect to pair your balance board.";
            Log("First launch: waiting for you to click Connect (no automatic pairing).");
            return;
        }

        if (_settings.AutoConnectOnStartup)
        {
            Log("Auto-reconnect: looking for a paired board…");
            BeginConnect(ConnectionIntent.QuickReconnect, quiet: true);
        }
    }

    public void OnActivatedFromSecondInstance()
    {
        Log("Another launch brought this window to the front.");
        if (_session.IsConnected || _connectInProgress)
        {
            return;
        }

        if (_settings.HasConnectedBefore && _settings.AutoConnectOnStartup)
        {
            BeginConnect(ConnectionIntent.QuickReconnect, quiet: true);
        }
    }

    private void HookSession()
    {
        _session.Processed += OnProcessed;
        _session.Log += Log;
        _session.StatusChanged += status => Dispatcher.Invoke(() =>
        {
            StatusText.Text = status;
            UpdateConnectionChip(_session.IsConnected);
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

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // _uiReady is set in ctor so deferred startup can persist settings.
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        CancelConnect();
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
    }

    private void UpdateSliderLabels()
    {
        DeadzoneLabel.Text = $"Deadzone: {DeadzoneSlider.Value:0}%";
        SensitivityLabel.Text = $"Sensitivity: {SensitivitySlider.Value:0.0}×";
        TriggerLeftRightLabel.Text = $"Left/right trigger: {TriggerLeftRightSlider.Value:0}%";
        TriggerForwardBackwardLabel.Text = $"Forward/back trigger: {TriggerForwardBackwardSlider.Value:0}%";
    }

    private void OnProcessed(ProcessedBalance data)
    {
        Dispatcher.Invoke(() =>
        {
            BoardVisual.Data = data;
            WeightText.Text = $"Weight: {data.WeightKg:0.0} kg";
            BalanceText.Text = $"Balance: {data.BalanceX:0.0}% / {data.BalanceY:0.0}%";
            DirectionText.Text = DescribeDirection(data);
            ActiveActionsText.Text = DescribeActiveInputs(data);
            SensorText.Text =
                $"TL {data.TopLeftKg:0.0}  TR {data.TopRightKg:0.0}  BL {data.BottomLeftKg:0.0}  BR {data.BottomRightKg:0.0}\n" +
                $"vJoy  X={data.JoyX,6}  Y={data.JoyY,6}  Z={data.JoyZ,6}";
        });
    }

    private static string DescribeDirection(ProcessedBalance data)
    {
        if (data.WeightKg < 5) return "Step on the board";
        var parts = new List<string>();
        if (data.MoveForward) parts.Add("forward");
        if (data.MoveBackward) parts.Add("backward");
        if (data.MoveLeft) parts.Add("left");
        if (data.MoveRight) parts.Add("right");
        if (data.Jump) parts.Add("jump");
        return parts.Count == 0 ? "Centered" : string.Join(" · ", parts);
    }

    private string DescribeActiveInputs(ProcessedBalance data)
    {
        if (data.WeightKg < 5) return $"Profile: {_settings.ActiveProfileName}";
        var active = new List<string>();
        if (data.MoveForward) active.Add("Forward");
        if (data.MoveBackward) active.Add("Backward");
        if (data.MoveLeft) active.Add("Left");
        if (data.MoveRight) active.Add("Right");
        if (data.Jump) active.Add("Jump");
        return active.Count == 0 ? "Centered" : $"Active: {string.Join(", ", active)}";
    }

    private void RefreshVJoyStatus()
    {
        var diag = VJoyDiagnostics.Inspect(_settings.VJoyDeviceId);
        VJoyStatusText.Text =
            $"Driver: {(diag.DriverEnabled ? "OK" : "MISSING")}\n" +
            $"Status: {diag.DeviceStatus}\n" +
            $"Axes: {diag.HasAxisX}/{diag.HasAxisY}/{diag.HasAxisZ}/{diag.HasAxisRx}/{diag.HasAxisRy}/{diag.HasAxisRz}\n" +
            (diag.Error ?? "OK");

        var ok = diag.DriverEnabled && diag.DeviceStatus is not "VJD_STAT_MISS" and not "VJD_STAT_BUSY";
        VJoyChipText.Text = ok ? "vJoy: ready" : "vJoy: check";
        VJoyChip.BorderBrush = ok ? (Brush)FindResource("Brush.Success") : (Brush)FindResource("Brush.Warning");
    }

    private void UpdateConnectionChip(bool connected)
    {
        ConnectionChipText.Text = connected ? "Board: connected" : "Board: offline";
        ConnectionChip.BorderBrush = connected
            ? (Brush)FindResource("Brush.Success")
            : (Brush)FindResource("Brush.CardBorder");
    }

    private void UpdateConnectUi(bool isBusy)
    {
        ConnectButton.IsEnabled = !isBusy;
        CancelButton.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
        DisconnectButton.IsEnabled = !isBusy && _session.IsConnected;
    }

    private void Log(string message) => _fileLog.Write(message);

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
        _settingsStore.Save(_settings);
        _session.LoadSettings(_settings);
        RefreshVJoyStatus();
    }

    private void SettingChanged(object sender, RoutedEventArgs e) => SaveSettingsFromUi();
    private void SettingChanged(object sender, SelectionChangedEventArgs e) => SaveSettingsFromUi();

    private void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        SaveSettingsFromUi();
        BeginConnect(ConnectionIntent.PairAndConnect);
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => CancelConnect();

    private void CancelConnect()
    {
        _connectCts?.Cancel();
        _session.CancelConnect();
        if (_connectInProgress)
        {
            StatusText.Text = "Cancelling…";
        }
    }

    private async void BeginConnect(ConnectionIntent intent, bool quiet = false)
    {
        if (_connectInProgress || _session.IsConnected) return;

        _connectInProgress = true;
        _connectCts = new CancellationTokenSource();
        UpdateConnectUi(isBusy: true);
        StatusText.Text = intent switch
        {
            ConnectionIntent.QuickReconnect => "Reconnecting to your balance board…",
            _ => "Searching — press SYNC on the board (red button under batteries)",
        };

        try
        {
            var token = _connectCts.Token;
            var connected = await Task.Run(
                () => StaThread.Run(() => _session.ConnectWithIntent(intent, cancellationToken: token)),
                token);

            if (connected)
            {
                MarkConnectedSuccessfully();
                StatusText.Text = "Connected — step on the board to play.";
                Log("Connected.");
            }
            else if (!token.IsCancellationRequested)
            {
                StatusText.Text = intent == ConnectionIntent.QuickReconnect
                    ? "Board offline — turn it on or press SYNC, then click Connect."
                    : "Not found — press SYNC, then Connect again.";
                if (!quiet)
                {
                    Log(StatusText.Text);
                }
            }
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Cancelled.";
        }
        catch (Exception ex)
        {
            Log($"Error: {ex.Message}");
            StatusText.Text = "Error — see session log.";
        }
        finally
        {
            _connectCts?.Dispose();
            _connectCts = null;
            _connectInProgress = false;
            UpdateConnectUi(isBusy: false);
            UpdateConnectionChip(_session.IsConnected);
        }
    }

    private void MarkConnectedSuccessfully()
    {
        _settings.HasConnectedBefore = true;
        _settings.SetupWizardCompleted = true;
        _settingsStore.Save(_settings);
    }

    private void DisconnectButton_Click(object sender, RoutedEventArgs e)
    {
        _session.Disconnect();
        StatusText.Text = "Disconnected.";
        UpdateConnectionChip(false);
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e) => Close();

    private void ProfileCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_uiReady || _suppressSettingEvents || ProfileCombo.SelectedItem is not string profile) return;
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
            ProfileCombo.SelectedItem = _settings.ActiveProfileName;
        _suppressSettingEvents = false;
        UpdateSliderLabels();
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
        var report = DiagnosticsReport.Run(_settings.VJoyDeviceId);
        _lastHealthReport = report.ToClipboardText();
        HealthSummaryText.Text = report.Summary;
        foreach (var line in report.Lines) Log(line);
        RefreshVJoyStatus();
    }

    private void CopyReportButton_Click(object sender, RoutedEventArgs e)
    {
        var text = string.IsNullOrWhiteSpace(_lastHealthReport) ? LogBox.Text : _lastHealthReport;
        if (!string.IsNullOrWhiteSpace(text)) Clipboard.SetText(text);
    }

    private void OpenLogFolderButton_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(_fileLog.LogDirectory);
        Process.Start(new ProcessStartInfo(_fileLog.LogDirectory) { UseShellExecute = true });
    }

    private void ClearLogViewButton_Click(object sender, RoutedEventArgs e) => LogBox.Clear();

    public void ForceShutdown()
    {
        if (_shutdownCompleted) return;
        _shutdownCompleted = true;
        CancelConnect();
        if (_uiReady) SaveSettingsFromUi();
        _session.Dispose();
        Log("Shutting down.");
    }

    protected override void OnClosed(EventArgs e)
    {
        ForceShutdown();
        base.OnClosed(e);
    }
}
