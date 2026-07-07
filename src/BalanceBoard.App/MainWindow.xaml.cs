using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using BalanceBoard.App.Controls;
using BalanceBoard.App.Services;
using BalanceBoard.Core.Models;
using BalanceBoard.Core.Services;
namespace BalanceBoard.App;

public partial class MainWindow : Window
{
    private readonly SettingsStore _settingsStore = new();
    private readonly BalanceBoardSession _session;
    private readonly FileLogService _fileLog;
    private readonly StartupOptions _startupOptions;
    private readonly AppSettings _settings = new();
    private readonly bool _uiReady;
    private bool _shutdownCompleted;
    private bool _connectInProgress;
    private CancellationTokenSource? _connectCts;
    private string _lastHealthReport = string.Empty;

    public MainWindow(StartupOptions startupOptions, FileLogService? fileLog = null, int competingProcessesStopped = 0)
    {
        _fileLog = fileLog ?? new FileLogService();
        _startupOptions = startupOptions;
        _session = startupOptions.SimulateBoard
            ? new BalanceBoardSession(connection: new SimulatedBalanceBoardConnection())
            : new BalanceBoardSession();
        _settings = _settingsStore.Load();
        _session.LoadSettings(_settings, initializeVJoy: false);
        InitializeComponent();
        ThemeManager.Apply(_settings.ThemePreference);
        _uiReady = true;
        HookSession();
        HookFileLog();
        ThemeManager.WatchSystemTheme(_settings.ThemePreference, pref =>
        {
            ThemeManager.Apply(pref);
            RefreshDynamicBrushes();
        });
        PopulateUi();
        RefreshDynamicBrushes();
        RefreshVJoyStatus();
        UpdateSliderLabels();
        UpdateConnectUi(isBusy: false);

        if (competingProcessesStopped > 0)
        {
            Log($"Stopped {competingProcessesStopped} competing process(es).");
        }

        Log("Ready.");
        _fileLog.WriteSessionHeader(_settingsStore.SettingsPath, _settings);
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

        if (!_settings.HasConnectedBefore && _settings.ActiveProfileName == ActionPresets.Default)
        {
            _session.ApplyControllerPreset();
            SyncUiFromSettings();
            _settingsStore.Save(_settings);
        }

        ThemeManager.Apply(_settings.ThemePreference);
        RefreshDynamicBrushes();

        var diag = VJoyDiagnostics.Inspect(_settings.VJoyDeviceId);
        Log($"vJoy: driver={(diag.DriverEnabled ? "OK" : "missing")}, status={diag.DeviceStatus}");
        RefreshVJoyStatus();

        if (connectOnLaunch)
        {
            Log(_startupOptions.SimulateBoard
                ? "Simulated board: auto-connecting."
                : "Launch flag --connect: starting full pairing flow.");
            BeginConnect(_startupOptions.SimulateBoard
                ? ConnectionIntent.QuickReconnect
                : ConnectionIntent.PairAndConnect);
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
        _session.Log += SafeLog;
        _session.StatusChanged += status => Dispatcher.BeginInvoke(() =>
        {
            try
            {
                SafeLog(status);
                StatusText.Text = status;
                var connected = false;
                try { connected = _session.IsConnected; } catch { }
                UpdateConnectionChip(connected);
            }
            catch (Exception ex)
            {
                _fileLog.WriteException(ex, "StatusChanged UI");
            }
        });
    }

    private void HookFileLog()
    {
        _fileLog.LineWritten += line => Dispatcher.BeginInvoke(() =>
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

        DetailLevelCombo.Items.Clear();
        DetailLevelCombo.Items.Add("Simple");
        DetailLevelCombo.Items.Add("Standard");
        DetailLevelCombo.Items.Add("Advanced");
        DetailLevelCombo.SelectedIndex = (int)_settings.UiDetailLevel;

        ThemeCombo.ItemsSource = new[] { ThemePreference.System, ThemePreference.Light, ThemePreference.Dark };
        ThemeCombo.SelectedItem = _settings.ThemePreference;

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
        JumpThresholdSlider.Value = _settings.JumpWeightThresholdKg;
        JumpHoldSlider.Value = _settings.JumpHoldSeconds;
        SimpleSensitivityCheck.IsChecked = _settings.UseSimpleSensitivity;
        InvertXCheck.IsChecked = _settings.InvertX;
        InvertYCheck.IsChecked = _settings.InvertY;

        UpdateSensitivityModeUi();
        UpdateJumpPresetButtons();
        UpdateProfileButtonStyles();
        ApplyDetailLevel();

        VJoyDeviceCombo.Items.Clear();
        for (uint i = 1; i <= 16; i++)
        {
            VJoyDeviceCombo.Items.Add(i);
        }

        VJoyDeviceCombo.SelectedItem = _settings.VJoyDeviceId;
        LoadActionBindingsFromSettings();
        _suppressSettingEvents = false;
    }

    private IEnumerable<(string Slot, ActionBindingRow Row)> ActionBindingUi() =>
    [
        (ActionSlots.Left, BindLeft),
        (ActionSlots.Right, BindRight),
        (ActionSlots.Forward, BindForward),
        (ActionSlots.Backward, BindBackward),
        (ActionSlots.Jump, BindJump),
        (ActionSlots.Modifier, BindModifier),
        (ActionSlots.DiagonalLeft, BindDiagonalLeft),
        (ActionSlots.DiagonalRight, BindDiagonalRight),
    ];

    private void LoadActionBindingsFromSettings()
    {
        EnsureActionSlots();
        foreach (var (slot, row) in ActionBindingUi())
        {
            row.LoadBinding(_settings.Actions[slot]);
        }
    }

    private void SaveActionBindingsToSettings()
    {
        EnsureActionSlots();
        foreach (var (slot, row) in ActionBindingUi())
        {
            _settings.Actions[slot] = row.GetBinding();
        }
    }

    private void EnsureActionSlots()
    {
        foreach (var slot in ActionSlots.All)
        {
            if (!_settings.Actions.ContainsKey(slot))
            {
                _settings.Actions[slot] = new ActionBinding();
            }
        }
    }

    private void ActionBinding_Changed(object sender, EventArgs e) => SaveSettingsFromUi();

    private void UpdateSliderLabels()
    {
        DeadzoneLabel.Text = $"Deadzone: {DeadzoneSlider.Value:0}%";
        SensitivityLabel.Text = $"Sensitivity: {SensitivitySlider.Value:0.0}×";
        TriggerLeftRightLabel.Text = $"Left/right trigger: {TriggerLeftRightSlider.Value:0}%";
        TriggerForwardBackwardLabel.Text = $"Forward/back trigger: {TriggerForwardBackwardSlider.Value:0}%";
        JumpThresholdLabel.Text = _settings.UiDetailLevel == UiDetailLevel.Simple
            ? JumpPresets.DisplayName(_settings.JumpLevel)
            : $"Jump threshold: {JumpThresholdSlider.Value:0.0} kg";
        JumpHoldLabel.Text = _settings.UiDetailLevel == UiDetailLevel.Simple
            ? $"Jump hold: {JumpHoldSlider.Value:0.1} s"
            : $"Jump hold: {JumpHoldSlider.Value:0.0} s";
    }

    private void UpdateSensitivityModeUi()
    {
        var simple = SimpleSensitivityCheck.IsChecked == true;
        SimpleSensitivityPanel.Visibility = simple ? Visibility.Visible : Visibility.Collapsed;
        var showAdvancedSliders = _settings.UiDetailLevel == UiDetailLevel.Advanced && !simple;
        AdvancedSensitivityPanel.Visibility = showAdvancedSliders ? Visibility.Visible : Visibility.Collapsed;
        UpdateSensitivityPresetButtons();
    }

    private void UpdateSensitivityPresetButtons()
    {
        SetPresetButtonStyle(SensitivityLowButton, _settings.SensitivityLevel == SensitivityLevel.Low);
        SetPresetButtonStyle(SensitivityMediumButton, _settings.SensitivityLevel == SensitivityLevel.Medium);
        SetPresetButtonStyle(SensitivityHighButton, _settings.SensitivityLevel == SensitivityLevel.High);
        SetPresetButtonStyle(SensitivityHighlyButton, _settings.SensitivityLevel == SensitivityLevel.HighlySensitive);
    }

    private void UpdateProfileButtonStyles()
    {
        try
        {
            var profile = _settings.ActiveProfileName;
            var minecraft = profile == ActionPresets.Minecraft;
            SetPresetButtonStyle(GamePresetButton, profile == ActionPresets.GameController);
            SetPresetButtonStyle(MinecraftPresetButton, minecraft);
            SetPresetButtonStyle(DesktopPresetButton, profile == ActionPresets.KeyboardMovement);
            SetPresetButtonStyle(PedalPresetButton, profile == ActionPresets.Pedal);
            SetPresetButtonStyle(MousePresetButton, profile == ActionPresets.BalanceMouse);

            var accent = minecraft
                ? FindThemeBrush("Brush.Minecraft", "#22C55E")
                : FindThemeBrush("Brush.CardBorder");
            ProfileCard.BorderBrush = accent;
            ProfileCard.BorderThickness = minecraft ? new Thickness(2) : new Thickness(1);
            BalancePanel.BorderBrush = minecraft ? accent : FindThemeBrush("Brush.CardBorder");
            BalancePanel.BorderThickness = minecraft ? new Thickness(2) : new Thickness(1);

            if (minecraft)
            {
                ProfileHintText.Text =
                    "Minecraft + Controlify: in Options → Controls → Controlify, bind vJoy Device 1. " +
                    "Lean maps to the left stick (move); lift one foot to jump (vJoy A). Use mouse/right stick for look.";
                ProfileHintText.Visibility = Visibility.Visible;
            }
            else
            {
                ProfileHintText.Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception ex)
        {
            _fileLog.WriteException(ex, "UpdateProfileButtonStyles");
        }
    }

    private static Brush FindThemeBrush(string key, string fallbackHex = "#E5E7EB")
    {
        try
        {
            if (Application.Current?.TryFindResource(key) is Brush appBrush)
            {
                return appBrush;
            }
        }
        catch
        {
            // TryFindResource can throw on bad keys — use fallback.
        }

        return ParseHexBrush(fallbackHex);
    }

    private static Brush ParseHexBrush(string hex)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex)!;
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
        catch
        {
            return Brushes.Gray;
        }
    }

    private static void SetPresetButtonStyle(Button button, bool active)
    {
        try
        {
            button.Style = (Style)button.FindResource(active ? "Button.Primary" : "Button.Secondary");
        }
        catch
        {
            // Style lookup failed — leave default button style.
        }
    }

    private void RefreshDynamicBrushes()
    {
        RefreshVJoyStatus();
        UpdateConnectionChip(_session.IsConnected);
        UpdateProfileButtonStyles();
        UpdateSensitivityPresetButtons();
        UpdateJumpPresetButtons();
        ApplyDetailLevel();
    }

    private void UpdateJumpPresetButtons()
    {
        SetPresetButtonStyle(JumpEasyButton, _settings.JumpLevel == JumpLevel.Easy);
        SetPresetButtonStyle(JumpNormalButton, _settings.JumpLevel == JumpLevel.Normal);
        SetPresetButtonStyle(JumpHardButton, _settings.JumpLevel == JumpLevel.Hard);
    }

    private void ApplyDetailLevel()
    {
        var detail = _settings.UiDetailLevel;
        var simple = detail == UiDetailLevel.Simple;
        var standard = detail == UiDetailLevel.Standard;
        var advanced = detail == UiDetailLevel.Advanced;

        DetailLevelDescription.Text = detail switch
        {
            UiDetailLevel.Simple => "Simple — pick a profile and sensitivity; we handle the rest.",
            UiDetailLevel.Standard => "Standard — theme, calibration, and invert options.",
            UiDetailLevel.Advanced => "Advanced — full sliders, vJoy, bindings, and diagnostics.",
            _ => string.Empty,
        };

        ThemeOptionsPanel.Visibility = simple ? Visibility.Collapsed : Visibility.Visible;
        CalibrationExpander.Visibility = simple ? Visibility.Collapsed : Visibility.Visible;
        InvertPanel.Visibility = simple ? Visibility.Collapsed : Visibility.Visible;
        AdvancedSensitivityPanel.Visibility = advanced && SimpleSensitivityCheck.IsChecked != true
            ? Visibility.Visible
            : Visibility.Collapsed;
        DiagnosticsExpander.Visibility = advanced ? Visibility.Visible : Visibility.Collapsed;
        SessionLogExpander.Visibility = advanced ? Visibility.Visible : Visibility.Collapsed;

        if (simple)
        {
            VJoyExpander.Visibility = Visibility.Collapsed;
            KeyboardExpander.Visibility = Visibility.Collapsed;
        }
        else
        {
            VJoyExpander.Visibility = Visibility.Visible;
            KeyboardExpander.Visibility = Visibility.Visible;
            if (standard)
            {
                VJoyExpander.IsExpanded = false;
                KeyboardExpander.IsExpanded = false;
            }
        }

        ResetCenterButton.Visibility = simple ? Visibility.Collapsed : Visibility.Visible;
        SensorText.Visibility = simple ? Visibility.Collapsed : Visibility.Visible;
        VJoyAxesText.Visibility = advanced ? Visibility.Visible : Visibility.Collapsed;
        BoardButtonText.Visibility = advanced ? Visibility.Visible : Visibility.Collapsed;
        SimpleJumpPanel.Visibility = Visibility.Visible;
        SimpleJumpLabel.Text = simple ? "How easy to jump" : "Jump difficulty";

        var touchHeight = simple ? 44.0 : 32.0;
        ConnectButton.MinHeight = touchHeight;
        DisconnectButton.MinHeight = touchHeight;
        TareButton.MinHeight = touchHeight;
        SetCenterButton.MinHeight = touchHeight;
        GamePresetButton.MinHeight = touchHeight;
        MinecraftPresetButton.MinHeight = touchHeight;
        DesktopPresetButton.MinHeight = touchHeight;
        PedalPresetButton.MinHeight = touchHeight;
        MousePresetButton.MinHeight = touchHeight;
        SensitivityLowButton.MinHeight = touchHeight;
        SensitivityMediumButton.MinHeight = touchHeight;
        SensitivityHighButton.MinHeight = touchHeight;
        SensitivityHighlyButton.MinHeight = touchHeight;
        JumpEasyButton.MinHeight = touchHeight;
        JumpNormalButton.MinHeight = touchHeight;
        JumpHardButton.MinHeight = touchHeight;
    }

    private void OnProcessed(ProcessedBalance data)
    {
        _ = Dispatcher.InvokeAsync(() =>
        {
            try
            {
                BoardVisual.Data = data;
                WeightText.Text = $"Weight: {data.WeightKg:0.0} kg";
                BalanceText.Text = $"Balance: {data.BalanceX:0.0}% / {data.BalanceY:0.0}%";
                if (data.Jump)
                {
                    DirectionText.Text = "Jump!";
                    DirectionText.FontSize = 28;
                    DirectionText.Foreground = FindThemeBrush("Brush.Success", "#16A34A");
                    BalancePanel.BorderBrush = FindThemeBrush("Brush.Success", "#16A34A");
                    BalancePanel.BorderThickness = new Thickness(3);
                }
                else
                {
                    DirectionText.Text = DescribeDirection(data);
                    DirectionText.FontSize = 20;
                    DirectionText.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Foreground");
                    var minecraft = _settings.ActiveProfileName == ActionPresets.Minecraft;
                    var accent = minecraft
                        ? FindThemeBrush("Brush.Minecraft", "#22C55E")
                        : FindThemeBrush("Brush.CardBorder");
                    BalancePanel.BorderBrush = minecraft ? accent : FindThemeBrush("Brush.CardBorder");
                    BalancePanel.BorderThickness = minecraft ? new Thickness(2) : new Thickness(1);
                }

                ActiveActionsText.Text = DescribeActiveInputs(data);
                SensorText.Text =
                    $"TL {data.TopLeftKg:0.0} kg  TR {data.TopRightKg:0.0} kg  BL {data.BottomLeftKg:0.0} kg  BR {data.BottomRightKg:0.0} kg";
                VJoyAxesText.Text = $"vJoy  X={data.JoyX,6}  Y={data.JoyY,6}  Z={data.JoyZ,6}  RX={data.JoyRx,6}";
                BoardButtonText.Text = DescribeBoardButton(data);
            }
            catch (Exception ex)
            {
                _fileLog.WriteException(ex, "OnProcessed UI");
            }
        }, DispatcherPriority.DataBind);
    }

    private string DescribeBoardButton(ProcessedBalance data)
    {
        if (data.Jump && _settings.MapJumpToVJoyButton)
        {
            return "vJoy A: jump";
        }

        if (data.VJoyButton1 && _settings.MapJumpToVJoyButton)
        {
            return "vJoy A: pressed";
        }

        return data.ButtonA ? "Board button: pressed (A)" : "Board button: up";
    }

    private static string DescribeDirection(ProcessedBalance data)
    {
        if (data.Jump)
        {
            return data.WeightKg < BalanceConstants.WeightOnBoardThresholdKg
                ? "Jump!"
                : "Jump! · " + DescribeLean(data);
        }

        if (data.WeightKg < BalanceConstants.WeightOnBoardThresholdKg)
        {
            return "Step on the board";
        }

        var lean = DescribeLean(data);
        return string.IsNullOrEmpty(lean) ? "Centered" : lean;
    }

    private static string DescribeLean(ProcessedBalance data)
    {
        var parts = new List<string>();
        if (data.MoveForward)
        {
            parts.Add("forward");
        }

        if (data.MoveBackward)
        {
            parts.Add("backward");
        }

        if (data.MoveLeft)
        {
            parts.Add("left");
        }

        if (data.MoveRight)
        {
            parts.Add("right");
        }

        return string.Join(" · ", parts);
    }

    private string DescribeActiveInputs(ProcessedBalance data)
    {
        if (data.Jump && data.WeightKg < BalanceConstants.WeightOnBoardThresholdKg)
        {
            return "Active: Jump";
        }

        if (data.WeightKg < BalanceConstants.WeightOnBoardThresholdKg)
        {
            return $"Profile: {_settings.ActiveProfileName}";
        }

        var active = new List<string>();
        if (data.MoveForward)
        {
            active.Add("Forward");
        }

        if (data.MoveBackward)
        {
            active.Add("Backward");
        }

        if (data.MoveLeft)
        {
            active.Add("Left");
        }

        if (data.MoveRight)
        {
            active.Add("Right");
        }

        if (data.Jump)
        {
            active.Add("Jump");
        }

        return active.Count == 0 ? "Centered" : $"Active: {string.Join(", ", active)}";
    }

    private void RefreshVJoyStatus()
    {
        try
        {
            var diag = VJoyDiagnostics.Inspect(_settings.VJoyDeviceId);
            VJoyStatusText.Text =
                $"Driver: {(diag.DriverEnabled ? "OK" : "MISSING")}\n" +
                $"Status: {diag.DeviceStatus}\n" +
                $"Axes: {diag.HasAxisX}/{diag.HasAxisY}/{diag.HasAxisZ}/{diag.HasAxisRx}/{diag.HasAxisRy}/{diag.HasAxisRz}\n" +
                (diag.Error ?? "OK");

            var ok = diag.DriverEnabled && diag.DeviceStatus is not "VJD_STAT_MISS" and not "VJD_STAT_BUSY";
            VJoyChipText.Text = ok ? "vJoy: ready" : "vJoy: check";
            VJoyChip.BorderBrush = ok
                ? FindThemeBrush("Brush.Success", "#22C55E")
                : FindThemeBrush("Brush.Warning", "#F59E0B");
        }
        catch (Exception ex)
        {
            _fileLog.WriteException(ex, "RefreshVJoyStatus");
        }
    }

    private void UpdateConnectionChip(bool connected)
    {
        try
        {
            ConnectionChipText.Text = connected ? "Board: connected" : "Board: offline";
            ConnectionChip.BorderBrush = connected
                ? FindThemeBrush("Brush.Success", "#22C55E")
                : FindThemeBrush("Brush.CardBorder");
        }
        catch (Exception ex)
        {
            _fileLog.WriteException(ex, "UpdateConnectionChip");
        }

        if (!connected)
        {
            ResetLiveReadoutPlaceholders();
        }
    }

    private void ResetLiveReadoutPlaceholders()
    {
        WeightText.Text = "Weight: — kg";
        BalanceText.Text = "Balance: — / —";
        DirectionText.Text = "Connect your board to begin";
        ActiveActionsText.Text = string.Empty;
        SensorText.Text = "Sensors: connect to see corner loads";
        VJoyAxesText.Text = string.Empty;
        BoardButtonText.Text = "Board button: —";
        BoardVisual.Data = null;
    }

    private void UpdateConnectUi(bool isBusy)
    {
        ConnectButton.IsEnabled = !isBusy;
        CancelButton.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
        DisconnectButton.IsEnabled = !isBusy && _session.IsConnected;
    }

    private void Log(string message) => SafeLog(message);

    private void SafeLog(string message)
    {
        try
        {
            _fileLog.Write(message);
        }
        catch
        {
            // Never crash from logging.
        }
    }

    private bool _suppressSettingEvents;

    private void SaveSettingsFromUi()
    {
        if (!_uiReady || _suppressSettingEvents)
        {
            return;
        }

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
        _settings.JumpWeightThresholdKg = (float)JumpThresholdSlider.Value;
        _settings.JumpHoldSeconds = JumpHoldSlider.Value;
        _settings.UseSimpleSensitivity = SimpleSensitivityCheck.IsChecked == true;
        if (ProfileCombo.SelectedItem is string profile)
        {
            _settings.ActiveProfileName = profile;
        }
        if (ThemeCombo.SelectedItem is ThemePreference theme)
        {
            _settings.ThemePreference = theme;
        }

        if (DetailLevelCombo.SelectedIndex is >= 0 and <= 2)
        {
            _settings.UiDetailLevel = (UiDetailLevel)DetailLevelCombo.SelectedIndex;
        }

        _settings.InvertX = InvertXCheck.IsChecked == true;
        _settings.InvertY = InvertYCheck.IsChecked == true;
        if (VJoyDeviceCombo.SelectedItem is uint id)
        {
            _settings.VJoyDeviceId = id;
        }
        else if (VJoyDeviceCombo.SelectedItem is int intId)
        {
            _settings.VJoyDeviceId = (uint)intId;
        }

        SaveActionBindingsToSettings();

        UpdateSliderLabels();
        UpdateSensitivityModeUi();
        ApplyDetailLevel();
        _settingsStore.Save(_settings);
        _session.LoadSettings(_settings);
        ThemeManager.Apply(_settings.ThemePreference);
        RefreshDynamicBrushes();
    }

    private void SettingChanged(object sender, RoutedEventArgs e) => SaveSettingsFromUi();
    private void SettingChanged(object sender, SelectionChangedEventArgs e) => SaveSettingsFromUi();

    private void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SaveSettingsFromUi();
            BeginConnect(ConnectionIntent.PairAndConnect);
        }
        catch (Exception ex)
        {
            _fileLog.WriteException(ex, "Connect button");
            SafeLog($"Error: {ex.Message}");
        }
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
        if (_connectInProgress || _session.IsConnected)
        {
            return;
        }

        Log($"[CONNECT] UI begin intent={intent} quiet={quiet}");
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
            var result = await _session.ConnectWithIntentAsync(intent, cancellationToken: token);

            if (result.IsSuccess)
            {
                MarkConnectedSuccessfully();
                StatusText.Text = "Connected — step on the board (use Tare if weight looks wrong).";
                Log(result.Message ?? "Connected.");
                if (!_startupOptions.SimulateBoard)
                {
                    System.Media.SystemSounds.Asterisk.Play();
                }

                ScheduleAutoExitIfRequested();
            }
            else if (!token.IsCancellationRequested)
            {
                StatusText.Text = result.Status switch
                {
                    ConnectStatus.Cancelled => "Cancelled.",
                    ConnectStatus.NoDevices => intent == ConnectionIntent.QuickReconnect
                        ? "Board offline — turn it on or press SYNC, then click Connect."
                        : "Not found — press SYNC, then Connect again.",
                    _ => result.Message ?? "Connection failed — see session log.",
                };
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
            _fileLog.WriteException(ex, "Connect");
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
            Log($"[CONNECT] UI end connected={_session.IsConnected}");
        }
    }

    private void MarkConnectedSuccessfully()
    {
        var deviceId = _session.ConnectedDeviceId;
        _settingsStore.UpdateConnectionState(_settings, deviceId);
        Log(deviceId is not null
            ? $"Saved connection state for board {deviceId}."
            : "Saved connection state.");
    }

    private void ScheduleAutoExitIfRequested()
    {
        if (_startupOptions.AutoExitAfterSeconds <= 0)
        {
            return;
        }

        Log($"Auto-exit in {_startupOptions.AutoExitAfterSeconds}s (--auto-exit-after).");
        _ = Task.Delay(TimeSpan.FromSeconds(_startupOptions.AutoExitAfterSeconds))
            .ContinueWith(_ => Dispatcher.BeginInvoke(Close));
    }

    private void DisconnectButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _session.Disconnect();
            StatusText.Text = "Disconnected.";
            UpdateConnectionChip(false);
            UpdateConnectUi(isBusy: false);
        }
        catch (Exception ex)
        {
            _fileLog.WriteException(ex, "Disconnect button");
            SafeLog($"Disconnect error: {ex.Message}");
        }
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e) => Close();

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
        AutoConnectCheck.IsChecked = _settings.AutoConnectOnStartup;
        AutoTareCheck.IsChecked = _settings.AutoTareOnConnect;
        TriggerLeftRightSlider.Value = _settings.TriggerLeftRight;
        TriggerForwardBackwardSlider.Value = _settings.TriggerForwardBackward;
        DeadzoneSlider.Value = _settings.DeadzonePercent;
        SensitivitySlider.Value = _settings.Sensitivity;
        JumpThresholdSlider.Value = _settings.JumpWeightThresholdKg;
        JumpHoldSlider.Value = _settings.JumpHoldSeconds;
        SimpleSensitivityCheck.IsChecked = _settings.UseSimpleSensitivity;
        InvertXCheck.IsChecked = _settings.InvertX;
        InvertYCheck.IsChecked = _settings.InvertY;
        ThemeCombo.SelectedItem = _settings.ThemePreference;
        DetailLevelCombo.SelectedIndex = (int)_settings.UiDetailLevel;
        if (ActionPresets.All.Contains(_settings.ActiveProfileName))
        {
            ProfileCombo.SelectedItem = _settings.ActiveProfileName;
        }

        if (VJoyDeviceCombo.Items.Contains(_settings.VJoyDeviceId))
        {
            VJoyDeviceCombo.SelectedItem = _settings.VJoyDeviceId;
        }

        LoadActionBindingsFromSettings();
        UpdateProfileButtonStyles();
        UpdateJumpPresetButtons();
        UpdateSensitivityModeUi();
        ApplyDetailLevel();
        _suppressSettingEvents = false;
        UpdateSliderLabels();
    }

    private void Tare_Click(object sender, RoutedEventArgs e) => RunSessionAction(_session.Tare, "Tare");
    private void SetCenter_Click(object sender, RoutedEventArgs e) => RunSessionAction(_session.SetCenter, "Set center");
    private void ResetCenter_Click(object sender, RoutedEventArgs e) => RunSessionAction(_session.ResetCenter, "Reset center");

    private void RunSessionAction(Action action, string label)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            _fileLog.WriteException(ex, label);
            SafeLog($"{label} error: {ex.Message}");
        }
    }

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

    private void MousePreset_Click(object sender, RoutedEventArgs e)
    {
        _session.ApplyMousePreset();
        SyncUiFromSettings();
        SaveSettingsFromUi();
    }

    private void MinecraftPreset_Click(object sender, RoutedEventArgs e)
    {
        _session.ApplyMinecraftPreset();
        SyncUiFromSettings();
        SaveSettingsFromUi();
    }

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => SaveSettingsFromUi();

    private void ApplySensitivityPreset(SensitivityLevel level)
    {
        SensitivityPresets.Apply(_settings, level);
        _suppressSettingEvents = true;
        TriggerLeftRightSlider.Value = _settings.TriggerLeftRight;
        TriggerForwardBackwardSlider.Value = _settings.TriggerForwardBackward;
        DeadzoneSlider.Value = _settings.DeadzonePercent;
        SensitivitySlider.Value = _settings.Sensitivity;
        _suppressSettingEvents = false;
        SaveSettingsFromUi();
        SyncUiFromSettings();
    }

    private void SensitivityLow_Click(object sender, RoutedEventArgs e) => ApplySensitivityPreset(SensitivityLevel.Low);
    private void SensitivityMedium_Click(object sender, RoutedEventArgs e) => ApplySensitivityPreset(SensitivityLevel.Medium);
    private void SensitivityHigh_Click(object sender, RoutedEventArgs e) => ApplySensitivityPreset(SensitivityLevel.High);
    private void SensitivityHighly_Click(object sender, RoutedEventArgs e) => ApplySensitivityPreset(SensitivityLevel.HighlySensitive);

    private void DetailLevelCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => SaveSettingsFromUi();

    private void ApplyJumpPreset(JumpLevel level)
    {
        JumpPresets.Apply(_settings, level);
        _suppressSettingEvents = true;
        JumpThresholdSlider.Value = _settings.JumpWeightThresholdKg;
        JumpHoldSlider.Value = _settings.JumpHoldSeconds;
        _suppressSettingEvents = false;
        SaveSettingsFromUi();
        SyncUiFromSettings();
    }

    private void JumpEasy_Click(object sender, RoutedEventArgs e) => ApplyJumpPreset(JumpLevel.Easy);
    private void JumpNormal_Click(object sender, RoutedEventArgs e) => ApplyJumpPreset(JumpLevel.Normal);
    private void JumpHard_Click(object sender, RoutedEventArgs e) => ApplyJumpPreset(JumpLevel.Hard);

    private void RunHealthCheckButton_Click(object sender, RoutedEventArgs e)
    {
        IReadOnlyList<string>? knownDevices = null;
        if (_session.IsConnected && _session.ConnectedDeviceId is not null)
        {
            knownDevices = [_session.ConnectedDeviceId];
        }

        var report = DiagnosticsReport.Run(_settings.VJoyDeviceId, knownDevices);
        _lastHealthReport = report.ToClipboardText();
        HealthSummaryText.Text = report.Summary;
        foreach (var line in report.Lines)
        {
            Log(line);
        }

        RefreshVJoyStatus();
    }

    private void CopyReportButton_Click(object sender, RoutedEventArgs e)
    {
        var text = string.IsNullOrWhiteSpace(_lastHealthReport) ? LogBox.Text : _lastHealthReport;
        if (!string.IsNullOrWhiteSpace(text))
        {
            Clipboard.SetText(text);
        }
    }

    private void OpenLogFolderButton_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(_fileLog.LogDirectory);
        Process.Start(new ProcessStartInfo(_fileLog.LogDirectory) { UseShellExecute = true });
    }

    private void ClearLogViewButton_Click(object sender, RoutedEventArgs e) => LogBox.Clear();

    public void ForceShutdown()
    {
        if (_shutdownCompleted)
        {
            return;
        }

        _shutdownCompleted = true;
        CancelConnect();
        if (_uiReady)
        {
            SaveSettingsFromUi();
        }

        try
        {
            _session.Dispose();
        }
        catch (Exception ex)
        {
            _fileLog.WriteException(ex, "Session dispose");
        }

        Log("Shutting down.");
    }

    protected override void OnClosed(EventArgs e)
    {
        ForceShutdown();
        base.OnClosed(e);
    }

    /// <summary>Used by UiSmoke to verify Minecraft preset styling does not throw.</summary>
    internal void SmokeApplyMinecraftPreset()
    {
        _session.ApplyMinecraftPreset();
        SyncUiFromSettings();
    }

    internal Brush? SmokeProfileCardBorderBrush => ProfileCard.BorderBrush;
}
