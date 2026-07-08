using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using BalanceBoard.App.Controls;
using BalanceBoard.App.Dialogs;
using BalanceBoard.App.Services;
using BalanceBoard.Core.Models;
using BalanceBoard.Core.Processing;
using BalanceBoard.Core.Services;
using Microsoft.Win32;
namespace BalanceBoard.App;

public partial class MainWindow : Window
{
    private readonly SettingsStore _settingsStore;
    private readonly BalanceBoardSession _session;
    private readonly FileLogService _fileLog;
    private readonly StartupOptions _startupOptions;
    private readonly AppSettings _settings;
    private readonly PhysicalTestRunner? _physicalTestRunner;
    private readonly bool _uiReady;
    private readonly SettingsSync _settingsSync;
    private bool _shutdownCompleted;
    private bool _connectInProgress;
    private CancellationTokenSource? _connectCts;
    private Task<ConnectResult>? _connectTask;
    private CancellationTokenSource? _autoExitCts;
    private string _lastHealthReport = string.Empty;
    private string _lastLogLine = string.Empty;
    private bool _physicalTestConnected;

    public MainWindow(
        StartupOptions startupOptions,
        FileLogService? fileLog = null,
        SettingsStore? settingsStore = null,
        BalanceBoardSession? session = null,
        int competingProcessesStopped = 0)
    {
        _fileLog = fileLog ?? new FileLogService();
        _startupOptions = startupOptions;
        _settingsStore = settingsStore ?? new SettingsStore();
        _session = session ?? (startupOptions.SimulateBoard
            ? new BalanceBoardSession(connection: new SimulatedBalanceBoardConnection())
            : new BalanceBoardSession());
        _settings = _settingsStore.Load();
        _session.LoadSettings(_settings, initializeVJoy: false);
        if (_startupOptions.HardwareTestMode)
        {
            _physicalTestRunner = new PhysicalTestRunner(
                PhysicalTestScenarioCatalog.Create(_startupOptions.PhysicalTestScenario!),
                _fileLog);
        }

        InitializeComponent();
        WireViewEvents();
        _settingsSync = CreateSettingsSync();
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
        InitializePhysicalTestMode();

        if (competingProcessesStopped > 0)
        {
            Log($"Stopped {competingProcessesStopped} competing process(es).");
        }

        Log("Ready.");
        _fileLog.WriteSessionHeader(_settingsStore.SettingsPath, _settings);
        if (_physicalTestRunner is not null)
        {
            Log($"[PHYSICAL] Scenario={_physicalTestRunner.Scenario.Id} artifacts={_physicalTestRunner.OutputDirectory}");
        }

        UpdateConnectionChip(ConnectionPhase.Offline);

        if (_settings.StartMinimized && _physicalTestRunner is null)
        {
            WindowState = WindowState.Minimized;
            Log("Starting minimized (Start minimized is on).");
        }
    }

    public void RunDeferredStartup(bool connectOnLaunch, int competingProcessesStopped = 0)
    {
        if (competingProcessesStopped > 0)
        {
            Log($"Startup cleanup stopped {competingProcessesStopped} competing process(es).");
        }

        BluetoothPairingService.Warmup();
        if (_startupOptions.SimulateBoard)
        {
            _settings.EnableVJoy = false;
        }

        _session.LoadSettings(_settings, initializeVJoy: _settings.EnableVJoy);

        if (!_settings.HasConnectedBefore && _settings.ActiveProfileName == ActionPresets.Default)
        {
            _session.ApplyMinecraftPreset();
            SyncUiFromSettings();
            _settingsStore.Save(_settings);
        }

        ThemeManager.Apply(_settings.ThemePreference);
        RefreshDynamicBrushes();

        var diag = VJoyDiagnostics.Inspect(_settings.VJoyDeviceId);
        if (_settings.EnableVJoy)
        {
            Log($"vJoy: driver={(diag.DriverEnabled ? "OK" : "missing")}, status={diag.DeviceStatus}");
        }
        else if (_startupOptions.SimulateBoard)
        {
            Log("vJoy: skipped for simulated board (--simulate-board).");
        }

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
        }
        else if (_settings.AutoConnectOnStartup)
        {
            Log("Auto-reconnect: looking for a paired board…");
            BeginConnect(ConnectionIntent.QuickReconnect, quiet: true);
        }

        ScheduleAutoExitIfRequested();
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
        _session.ConnectionPhaseChanged += phase => Dispatcher.BeginInvoke(() =>
        {
            try
            {
                _physicalTestConnected = phase == ConnectionPhase.Connected;
                UpdateConnectionChip(phase);
                UpdatePhysicalTestObservation();
            }
            catch (Exception ex)
            {
                _fileLog.WriteException(ex, "ConnectionPhaseChanged UI");
            }
        });
        _session.StatusChanged += status => Dispatcher.BeginInvoke(() =>
        {
            try
            {
                SafeLog(status);
                StatusText.Text = ConnectionStatusText.FormatStatusForUser(status, _settings.UiDetailLevel);
                UpdatePhysicalTestObservation();
                // Do not read ConnectionPhase here — connect runs on ConnectionWorker and
                // TryInvoke from the UI thread would deadlock while StatusChanged fires mid-connect.
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
            _lastLogLine = line;
            UpdateLogPreview();
        });
    }

    private void InitializePhysicalTestMode()
    {
        if (_physicalTestRunner is null)
        {
            PhysicalTestPanel.Visibility = Visibility.Collapsed;
            return;
        }

        PhysicalTestPanel.Visibility = Visibility.Visible;
        PhysicalTestScenarioText.Text =
            $"{_physicalTestRunner.Scenario.DisplayName} ({_physicalTestRunner.Scenario.Id})" + Environment.NewLine +
            _physicalTestRunner.Scenario.Description;
        PhysicalTestArtifactsText.Text = $"Artifacts: {_physicalTestRunner.OutputDirectory}";
        UpdatePhysicalTestUi();
    }

    private void UpdatePhysicalTestObservation(ProcessedBalance? data = null)
    {
        if (_physicalTestRunner is null)
        {
            return;
        }

        _physicalTestRunner.UpdateObservation(new PhysicalTestObservation
        {
            TimestampUtc = DateTime.UtcNow,
            IsConnected = _physicalTestConnected,
            WeightKg = data?.WeightKg ?? 0,
            BalanceX = data?.BalanceX ?? 0,
            BalanceY = data?.BalanceY ?? 0,
            JumpDetected = data?.Jump ?? false,
            StatusText = StatusText.Text,
        });
        UpdatePhysicalTestUi();
    }

    private void UpdatePhysicalTestUi()
    {
        if (_physicalTestRunner is null)
        {
            return;
        }

        if (_physicalTestRunner.CurrentStep is { } currentStep)
        {
            PhysicalTestStepTitleText.Text = currentStep.Title;
            PhysicalTestStepInstructionsText.Text = currentStep.Instructions;
            PhysicalTestExpectedSignalText.Text = string.IsNullOrWhiteSpace(currentStep.ExpectedSignal)
                ? string.Empty
                : $"Expected: {currentStep.ExpectedSignal}";
        }
        else
        {
            PhysicalTestStepTitleText.Text = "Run complete";
            PhysicalTestStepInstructionsText.Text = "Review the artifact folder for the structured result and event trace.";
            PhysicalTestExpectedSignalText.Text = $"Final status: {_physicalTestRunner.OverallStatus}";
        }

        var completed = _physicalTestRunner.Steps.Count(step => step.Outcome != PhysicalTestStepOutcome.Pending);
        PhysicalTestProgressText.Text =
            $"Progress: {completed}/{_physicalTestRunner.Steps.Count} steps  |  Run status: {_physicalTestRunner.OverallStatus}";

        var interactive = !_physicalTestRunner.IsComplete;
        PhysicalTestPassButton.IsEnabled = interactive;
        PhysicalTestFailButton.IsEnabled = interactive;
        PhysicalTestSkipButton.IsEnabled = interactive;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // _uiReady is set in ctor so deferred startup can persist settings.
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        CancelConnect();
    }

    private SettingsSync CreateSettingsSync() =>
        new(_settings, new SettingsControls
        {
            ProfileCombo = ProfileCombo,
            DetailLevelCombo = DetailLevelCombo,
            ThemeCombo = ThemeCombo,
            OutputModeCombo = OutputModeCombo,
            JumpVJoyButtonCombo = JumpVJoyButtonCombo,
            VJoyDeviceCombo = VJoyDeviceCombo,
            ResponseCurveCombo = ResponseCurveCombo,
            SendCgCheck = SendCgCheck,
            SendSensorsCheck = SendSensorsCheck,
            MapJumpVJoyCheck = MapJumpVJoyCheck,
            AutoConnectCheck = AutoConnectCheck,
            StartMinimizedCheck = StartMinimizedCheck,
            AutoTareCheck = AutoTareCheck,
            SimpleSensitivityCheck = SimpleSensitivityCheck,
            OneFootModeCheck = OneFootModeCheck,
            InvertXCheck = InvertXCheck,
            InvertYCheck = InvertYCheck,
            LockLeftRightAxisCheck = LockLeftRightAxisCheck,
            LockForwardBackwardAxisCheck = LockForwardBackwardAxisCheck,
            SplitAxisSensitivityCheck = SplitAxisSensitivityCheck,
            SplitAxisDeadzoneCheck = SplitAxisDeadzoneCheck,
            PollIntervalSlider = PollIntervalSlider,
            TriggerLeftRightSlider = TriggerLeftRightSlider,
            TriggerForwardBackwardSlider = TriggerForwardBackwardSlider,
            DeadzoneSlider = DeadzoneSlider,
            SensitivitySlider = SensitivitySlider,
            JumpThresholdSlider = JumpThresholdSlider,
            JumpHoldSlider = JumpHoldSlider,
            SensitivityLeftRightSlider = SensitivityLeftRightSlider,
            SensitivityForwardBackwardSlider = SensitivityForwardBackwardSlider,
            DeadzoneLeftRightSlider = DeadzoneLeftRightSlider,
            DeadzoneForwardBackwardSlider = DeadzoneForwardBackwardSlider,
            SessionLogExpander = SessionLogExpander,
            JumpVJoyPanel = JumpVJoyPanel,
        });

    private void PopulateUi()
    {
        _suppressSettingEvents = true;
        _settingsSync.InitializeComboItems();
        _settingsSync.ApplyToUi();
        UpdateSensitivityModeUi();
        UpdateJumpPresetButtons();
        UpdateProfileButtonStyles();
        ApplyDetailLevel();
        RefreshVJoyDeviceCombo();
        BindBoardButton.IncludeVJoyButton = true;
        LoadActionBindingsFromSettings();
        RefreshCustomProfiles();
        UpdateSessionLogUi();
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
        (ActionSlots.BoardButton, BindBoardButton),
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

    private void ActionBinding_Changed(object sender, EventArgs e)
    {
        if (_uiReady
            && !_suppressSettingEvents
            && sender is ActionBindingRow row
            && row != BindBoardButton
            && row.GetBinding().Kind != ActionKind.None
            && _settings.OutputMode == OutputMode.VJoy)
        {
            _suppressSettingEvents = true;
            _settings.SetOutputMode(OutputMode.Keyboard);
            _settingsSync.PopulateOutputModeCombo();
            _suppressSettingEvents = false;
            Log("Switched movement output to keyboard because a movement binding was set.");
        }

        SaveSettingsFromUi();
    }

    private bool HasAnyActiveBinding() =>
        ActionBindingUi().Any(entry => entry.Row != BindBoardButton && entry.Row.GetBinding().Kind != ActionKind.None);

    private void UpdateSliderLabels()
    {
        DeadzoneLabel.Text = $"Deadzone: {DeadzoneSlider.Value:0}%";
        var sens = SensitivitySlider.Value;
        var leanForFull = sens >= 0.25 ? 50.0 / sens : 50.0;
        SensitivityLabel.Text = $"Sensitivity: {sens:0.0}× (~{leanForFull:0.#}% lean → full stick)";

        var sensX = SensitivityLeftRightSlider.Value > 0 ? SensitivityLeftRightSlider.Value : sens;
        var sensY = SensitivityForwardBackwardSlider.Value > 0 ? SensitivityForwardBackwardSlider.Value : sens;
        SensitivityLeftRightLabel.Text =
            $"Left / right sensitivity: {(SensitivityLeftRightSlider.Value > 0 ? $"{sensX:0.0}×" : "same as main")}";
        SensitivityForwardBackwardLabel.Text =
            $"Forward / back sensitivity: {(SensitivityForwardBackwardSlider.Value > 0 ? $"{sensY:0.0}×" : "same as main")}";

        var mainDz = DeadzoneSlider.Value;
        var splitDz = SplitAxisDeadzoneCheck.IsChecked == true;
        DeadzoneLeftRightLabel.Text = splitDz
            ? $"Left / right deadzone: {DeadzoneLeftRightSlider.Value:0}%"
            : $"Left / right deadzone: {mainDz:0}% (main)";
        DeadzoneForwardBackwardLabel.Text = splitDz
            ? $"Forward / back deadzone: {DeadzoneForwardBackwardSlider.Value:0}%"
            : $"Forward / back deadzone: {mainDz:0}% (main)";
        var pollMs = PollIntervalSlider.Value;
        PollIntervalLabel.Text = $"Update rate: {pollMs:0} ms (~{(pollMs > 0 ? 1000.0 / pollMs : 0):0} Hz)";
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
        var useSimplePresets = SimpleSensitivityCheck.IsChecked == true;
        var detail = _settings.UiDetailLevel;
        var showTuning = !useSimplePresets && detail >= UiDetailLevel.Standard;
        var showAdvancedTuning = !useSimplePresets && detail == UiDetailLevel.Advanced;

        SimpleSensitivityPanel.Visibility = useSimplePresets ? Visibility.Visible : Visibility.Collapsed;
        StandardSensitivityPanel.Visibility = showTuning ? Visibility.Visible : Visibility.Collapsed;
        AdvancedSensitivityPanel.Visibility = showAdvancedTuning ? Visibility.Visible : Visibility.Collapsed;
        FineTuneHintText.Visibility = useSimplePresets ? Visibility.Visible : Visibility.Collapsed;
        SplitAxisDeadzonePanel.Visibility =
            showTuning && SplitAxisDeadzoneCheck.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        SplitAxisSensitivityPanel.Visibility =
            showTuning && SplitAxisSensitivityCheck.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        UpdateSensitivityPresetButtons();
    }

    private void UpdateSensitivityPresetButtons()
    {
        SetPresetButtonStyle(SensitivityLowButton, _settings.SensitivityLevel == SensitivityLevel.Low);
        SetPresetButtonStyle(SensitivityMediumButton, _settings.SensitivityLevel == SensitivityLevel.Medium);
        SetPresetButtonStyle(SensitivityHighButton, _settings.SensitivityLevel == SensitivityLevel.High);
        SetPresetButtonStyle(SensitivityHighlyButton, _settings.SensitivityLevel == SensitivityLevel.HighlySensitive);
        SetPresetButtonStyle(SensitivityHairTriggerButton, _settings.SensitivityLevel == SensitivityLevel.HairTrigger);
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
                    "Minecraft keyboard: lean to move (WASD), lift one foot to jump (Space). " +
                    "For Controlify/vJoy instead, pick “Minecraft (Controlify)” in the profile list.";
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
        ThemeManager.RefreshThemedElements(this);
        RefreshVJoyStatus();
        UpdateConnectionChip(_session.ConnectionPhase);
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
        var advanced = detail == UiDetailLevel.Advanced;
        var showAdvancedTab = !simple;

        DetailLevelDescription.Text = detail switch
        {
            UiDetailLevel.Simple => "Simple — Dashboard, Profiles, and Fine Tuning presets; we handle the rest.",
            UiDetailLevel.Standard => "Standard — presets or sliders on Fine Tuning; vJoy and bindings on Advanced.",
            UiDetailLevel.Advanced => "Advanced — full sliders on Fine Tuning; response curves, vJoy, bindings, and diagnostics on Advanced.",
            _ => string.Empty,
        };

        AdvancedTab.Visibility = showAdvancedTab ? Visibility.Visible : Visibility.Collapsed;
        FineTuningTab.Visibility = Visibility.Visible;
        if (!showAdvancedTab && MainTabControl.SelectedItem == AdvancedTab)
        {
            MainTabControl.SelectedIndex = 0;
        }

        ThemeCard.Visibility = simple ? Visibility.Collapsed : Visibility.Visible;
        CalibrationSection.Visibility = simple ? Visibility.Collapsed : Visibility.Visible;
        FineTuneSection.Visibility = simple ? Visibility.Collapsed : Visibility.Visible;
        UpdateSensitivityModeUi();
        DiagnosticsSection.Visibility = advanced ? Visibility.Visible : Visibility.Collapsed;
        LiveLogPanel.Visibility = simple ? Visibility.Collapsed : Visibility.Visible;
        ShowLogButton.Visibility = simple ? Visibility.Collapsed : Visibility.Visible;
        UpdateSessionLogUi();

        if (simple)
        {
            VJoySection.Visibility = Visibility.Collapsed;
            KeyboardSection.Visibility = Visibility.Collapsed;
        }
        else
        {
            VJoySection.Visibility = Visibility.Visible;
            KeyboardSection.Visibility = Visibility.Visible;
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
        SensitivityHairTriggerButton.MinHeight = touchHeight;
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
                    DirectionText.Text = BalanceReadoutText.DescribeDirection(data);
                    DirectionText.FontSize = 20;
                    DirectionText.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Foreground");
                    var minecraft = _settings.ActiveProfileName == ActionPresets.Minecraft;
                    var accent = minecraft
                        ? FindThemeBrush("Brush.Minecraft", "#22C55E")
                        : FindThemeBrush("Brush.CardBorder");
                    BalancePanel.BorderBrush = minecraft ? accent : FindThemeBrush("Brush.CardBorder");
                    BalancePanel.BorderThickness = minecraft ? new Thickness(2) : new Thickness(1);
                }

                ActiveActionsText.Text = BalanceReadoutText.DescribeActiveInputs(data, _settings);
                SensorText.Text =
                    $"TL {data.TopLeftKg:0.0} kg  TR {data.TopRightKg:0.0} kg  BL {data.BottomLeftKg:0.0} kg  BR {data.BottomRightKg:0.0} kg";
                VJoyAxesText.Text = $"vJoy  X={data.JoyX,6}  Y={data.JoyY,6}  Z={data.JoyZ,6}  RX={data.JoyRx,6}";
                BoardButtonText.Text = BalanceReadoutText.DescribeBoardButton(data, _settings);
                UpdatePhysicalTestObservation(data);
            }
            catch (Exception ex)
            {
                _fileLog.WriteException(ex, "OnProcessed UI");
            }
        }, DispatcherPriority.DataBind);
    }

    private void RefreshVJoyStatus()
    {
        try
        {
            var diag = VJoyDiagnostics.Inspect(_settings.VJoyDeviceId);
            VJoyStatusText.Text =
                $"Driver: {(diag.DriverEnabled ? "OK" : "MISSING")}\n" +
                $"Status: {diag.DeviceStatus}\n" +
                $"Product: {diag.Product ?? "—"}\n" +
                $"Buttons configured: {diag.ButtonCount}\n" +
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

    private void ConfigureVJoyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var configExe = VJoyConfigLocator.FindConfigExe();
            if (configExe is null)
            {
                Log("vJoy Configure tool not found. Install/repair vJoy, then look for 'Configure vJoy' in the Start menu.");
                MessageBox.Show(
                    this,
                    "Could not find vJoy's Configure tool (vJoyConf.exe).\n\n" +
                    "vJoy joysticks are set up in vJoy's own Configure utility that installs with the driver:\n" +
                    "  • Open the Start menu and search for \"Configure vJoy\", or\n" +
                    "  • Reinstall vJoy from the official download, then reboot.\n\n" +
                    "In that tool, enable at least axes X and Y for the device number selected here, then click Apply.",
                    "Configure vJoy",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            Log($"Opening vJoy Configure tool: {configExe}");
            Process.Start(new ProcessStartInfo(configExe) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _fileLog.WriteException(ex, "ConfigureVJoy");
            SafeLog($"Could not open vJoy Configure: {ex.Message}");
        }
    }

    private void GetVJoyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://github.com/njz3/vJoy/releases") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _fileLog.WriteException(ex, "GetVJoy");
            SafeLog($"Could not open vJoy download page: {ex.Message}");
        }
    }

    private void UpdateConnectionChip(ConnectionPhase phase)
    {
        try
        {
            var simple = _settings.UiDetailLevel == UiDetailLevel.Simple;
            ConnectionChipText.Text = phase switch
            {
                ConnectionPhase.Connected => simple ? "Board: ready!" : "Board: connected",
                ConnectionPhase.Connecting => simple ? "Board: finding…" : "Board: connecting…",
                ConnectionPhase.Reconnecting => simple ? "Board: trying again…" : "Board: reconnecting…",
                ConnectionPhase.PairedReconnecting => simple ? "Board: waiting for Bluetooth…" : "Board: paired, reconnecting…",
                _ => simple ? "Board: not connected" : "Board: offline",
            };

            ConnectionChip.BorderBrush = phase == ConnectionPhase.Connected
                ? FindThemeBrush("Brush.Success", "#22C55E")
                : phase is ConnectionPhase.Connecting or ConnectionPhase.Reconnecting or ConnectionPhase.PairedReconnecting
                    ? FindThemeBrush("Brush.Warning", "#F59E0B")
                    : FindThemeBrush("Brush.CardBorder");
        }
        catch (Exception ex)
        {
            _fileLog.WriteException(ex, "UpdateConnectionChip");
        }

        if (phase == ConnectionPhase.Offline)
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
        DisconnectButton.IsEnabled = !isBusy && !_connectInProgress && _session.IsConnected;
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
    private DispatcherTimer? _settingsSaveDebounceTimer;
    private const int SettingsSaveDebounceMs = 200;

    private void RequestSaveSettingsFromUi(bool immediate = false)
    {
        if (!_uiReady || _suppressSettingEvents)
        {
            return;
        }

        if (immediate)
        {
            CancelPendingSettingsSave();
            SaveSettingsFromUi();
            return;
        }

        _settingsSaveDebounceTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(SettingsSaveDebounceMs) };
        _settingsSaveDebounceTimer.Stop();
        _settingsSaveDebounceTimer.Tick -= OnDebouncedSettingsSave;
        _settingsSaveDebounceTimer.Tick += OnDebouncedSettingsSave;
        _settingsSaveDebounceTimer.Start();
    }

    private void OnDebouncedSettingsSave(object? sender, EventArgs e) { CancelPendingSettingsSave(); SaveSettingsFromUi(); }

    private void CancelPendingSettingsSave()
    {
        if (_settingsSaveDebounceTimer is null)
        {
            return;
        }

        _settingsSaveDebounceTimer.Stop();
        _settingsSaveDebounceTimer.Tick -= OnDebouncedSettingsSave;
    }

    private void SaveSettingsFromUi()
    {
        if (!_uiReady || _suppressSettingEvents)
        {
            return;
        }

        CancelPendingSettingsSave();
        _settingsSync.ReadFromUi();
        SaveActionBindingsToSettings();
        UpdateSliderLabels();
        UpdateSensitivityModeUi();
        ApplyDetailLevel();
        _settingsStore.Save(_settings);
        _session.LoadSettings(_settings);
        ThemeManager.Apply(_settings.ThemePreference);
        RefreshDynamicBrushes();
        RefreshVJoyStatus();
        UpdateVJoyDeviceDetails(VJoyDeviceCombo.SelectedItem as VJoyDeviceInfo);
    }

    private void RefreshVJoyDeviceCombo()
    {
        var devices = VJoyDiagnostics.ListConfiguredDevices();
        if (devices.Count == 0)
        {
            devices = [.. Enumerable.Range(1, 16).Select(id => new VJoyDeviceInfo
            {
                DeviceId = (uint)id,
                Status = "Unknown",
            })];
        }

        VJoyDeviceCombo.ItemsSource = devices;
        VJoyDeviceCombo.DisplayMemberPath = nameof(VJoyDeviceInfo.Summary);
        var selected = devices.FirstOrDefault(d => d.DeviceId == _settings.VJoyDeviceId) ?? devices[0];
        VJoyDeviceCombo.SelectedItem = selected;
        UpdateVJoyDeviceDetails(selected);
    }

    private void UpdateVJoyDeviceDetails(VJoyDeviceInfo? device)
    {
        if (device is null)
        {
            VJoyDeviceDetailsText.Text = "No vJoy device selected.";
            return;
        }

        VJoyDeviceDetailsText.Text =
            $"{device.Summary}\n" +
            $"RX {(device.HasAxisRx ? "✓" : "·")}  RY {(device.HasAxisRy ? "✓" : "·")}  RZ {(device.HasAxisRz ? "✓" : "·")}";
    }

    private void OutputModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_uiReady || _suppressSettingEvents)
        {
            return;
        }

        RequestSaveSettingsFromUi(immediate: true);
    }

    private void VJoyDeviceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_uiReady || _suppressSettingEvents)
        {
            return;
        }

        UpdateVJoyDeviceDetails(VJoyDeviceCombo.SelectedItem as VJoyDeviceInfo);
        RequestSaveSettingsFromUi(immediate: true);
        RefreshVJoyStatus();
    }

    private void SettingChanged(object sender, RoutedEventArgs e) => RequestSaveSettingsFromUi(immediate: true);
    private void SettingChanged(object sender, SelectionChangedEventArgs e) => RequestSaveSettingsFromUi(immediate: true);
    private void SliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => RequestSaveSettingsFromUi();

    private void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SaveSettingsFromUi();
            // Manual Connect always runs full pairing (WiiBalanceWalker: BT add + HID connect).
            // QuickReconnect is reserved for auto-connect on startup.
            const ConnectionIntent intent = ConnectionIntent.PairAndConnect;
            BeginConnect(intent);
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

    /// <summary>
    /// When multiple HID boards are already visible and no preferred id matches, ask the user which to use.
    /// Auto/quiet reconnects skip the picker and fall back to index 0.
    /// </summary>
    private int ResolveConnectDeviceIndex(ConnectionIntent intent, bool quiet)
    {
        try
        {
            var devices = _session.DiscoverDevices();
            if (devices.Count <= 1)
            {
                return 0;
            }

            var preferred = _settings.LastConnectedDeviceId;
            var allowDefault = quiet || intent == ConnectionIntent.QuickReconnect;
            var resolved = DeviceSelection.ResolveDeviceIndex(devices, preferred, allowDefault);
            if (resolved is not null)
            {
                Log($"[CONNECT] Multi-device: using index {resolved.Value} of {devices.Count} (preferred={preferred ?? "none"}).");
                return resolved.Value;
            }

            var picker = new DevicePickerDialog(devices, preferred) { Owner = this };
            if (picker.ShowDialog() == true && picker.SelectedIndex >= 0)
            {
                Log($"[CONNECT] Multi-device: user selected index {picker.SelectedIndex} ({picker.SelectedDeviceId}).");
                return picker.SelectedIndex;
            }

            Log("[CONNECT] Multi-device picker cancelled — using first board.");
            return 0;
        }
        catch (Exception ex)
        {
            _fileLog.WriteException(ex, "ResolveConnectDeviceIndex");
            return 0;
        }
    }

    private async void BeginConnect(ConnectionIntent intent, bool quiet = false)
    {
        if (_shutdownCompleted
            || _connectInProgress
            || _session.ConnectionPhase is ConnectionPhase.Connected or ConnectionPhase.Connecting)
        {
            return;
        }

        // Engage the in-progress guard and disable Connect *before* ResolveConnectDeviceIndex,
        // which can pump a nested Dispatcher loop via the multi-device picker's modal
        // ShowDialog(). Without this ordering, a concurrent BeginConnect (startup auto-connect,
        // second-instance activation) could slip past the guard above while this call is still
        // synchronously blocked waiting on the picker, launching two overlapping connects that
        // stomp each other's _connectCts/_connectTask/UI state.
        _connectInProgress = true;
        UpdateConnectUi(isBusy: true);

        var deviceIndex = ResolveConnectDeviceIndex(intent, quiet);

        Log($"[CONNECT] UI begin intent={intent} quiet={quiet} deviceIndex={deviceIndex}");
        _connectCts = new CancellationTokenSource();
        StatusText.Text = intent switch
        {
            ConnectionIntent.QuickReconnect => _settings.UiDetailLevel == UiDetailLevel.Simple
                ? "Finding board…"
                : "Reconnecting to your balance board…",
            _ => _settings.HasConnectedBefore
                ? "Finding board…"
                : "Searching — press SYNC on the board (red button under batteries)",
        };

        try
        {
            var token = _connectCts.Token;
            _connectTask = _session.ConnectWithIntentAsync(intent, deviceIndex, cancellationToken: token);
            var result = await _connectTask;

            if (_shutdownCompleted)
            {
                return;
            }

            if (result.IsSuccess)
            {
                MarkConnectedSuccessfully();
                StatusText.Text = _session.ConnectionPhase == ConnectionPhase.Connected
                    ? (_settings.UiDetailLevel == UiDetailLevel.Simple
                        ? "Connected!"
                        : "Connected — step on the board (use Tare if weight looks wrong).")
                    : "Finding board…";
                Log(result.Message ?? "Connected.");
                if (!_startupOptions.SimulateBoard)
                {
                    System.Media.SystemSounds.Asterisk.Play();
                }

                ScheduleAutoExitIfRequested();
            }
            else if (!token.IsCancellationRequested)
            {
                StatusText.Text = ConnectionStatusText.FormatConnectFailure(intent, result, _settings.UiDetailLevel);
                if (!quiet)
                {
                    Log(result.Message ?? StatusText.Text);
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
            _connectTask = null;
            _connectInProgress = false;
            if (!_shutdownCompleted)
            {
                UpdateConnectUi(isBusy: false);
                UpdateConnectionChip(_session.ConnectionPhase);
                Log($"[CONNECT] UI end connected={_session.IsConnected}");
            }
        }
    }

    private void MarkConnectedSuccessfully()
    {
        var deviceId = _session.ConnectedDeviceId;
        var adapterMac = _session.Settings.LastBluetoothAdapterMac;
        _settingsStore.UpdateConnectionState(_settings, deviceId, adapterMac);
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

        _autoExitCts?.Cancel();
        _autoExitCts?.Dispose();
        _autoExitCts = new CancellationTokenSource();
        Log($"Auto-exit in {_startupOptions.AutoExitAfterSeconds}s (--auto-exit-after).");
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_startupOptions.AutoExitAfterSeconds), _autoExitCts.Token);
                await Dispatcher.InvokeAsync(() =>
                {
                    if (!_shutdownCompleted)
                    {
                        Close();
                    }
                });
            }
            catch (OperationCanceledException)
            {
                // Window is shutting down or a new auto-exit timer replaced this one.
            }
        });
    }

    private void DisconnectButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _session.Disconnect();
            StatusText.Text = "Disconnected.";
            UpdateConnectionChip(ConnectionPhase.Offline);
            UpdateConnectUi(isBusy: false);
            UpdatePhysicalTestObservation();
        }
        catch (Exception ex)
        {
            _fileLog.WriteException(ex, "Disconnect button");
            SafeLog($"Disconnect error: {ex.Message}");
        }
    }

    private void PhysicalTestPassButton_Click(object sender, RoutedEventArgs e)
    {
        _physicalTestRunner?.MarkPassed("Confirmed manually in guided physical test mode.");
        UpdatePhysicalTestUi();
    }

    private void PhysicalTestFailButton_Click(object sender, RoutedEventArgs e)
    {
        _physicalTestRunner?.MarkFailed("Marked failed by tester from guided physical test mode.");
        UpdatePhysicalTestUi();
    }

    private void PhysicalTestSkipButton_Click(object sender, RoutedEventArgs e)
    {
        _physicalTestRunner?.MarkSkipped("Skipped by tester.");
        UpdatePhysicalTestUi();
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

    private void RefreshCustomProfiles(string? selectName = null)
    {
        try
        {
            var previous = selectName ?? CustomProfileCombo.SelectedItem as string;
            var profiles = _settingsStore.ListProfiles();
            var wasSuppressed = _suppressSettingEvents;
            _suppressSettingEvents = true;
            CustomProfileCombo.ItemsSource = profiles;
            if (previous is not null && profiles.Contains(previous))
            {
                CustomProfileCombo.SelectedItem = previous;
            }
            else if (profiles.Count > 0)
            {
                CustomProfileCombo.SelectedIndex = 0;
            }

            _suppressSettingEvents = wasSuppressed;

            var hasSelection = CustomProfileCombo.SelectedItem is string;
            LoadProfileButton.IsEnabled = hasSelection;
            DeleteProfileButton.IsEnabled = hasSelection;
            UpdateProfileButton.IsEnabled = hasSelection;
        }
        catch (Exception ex)
        {
            _fileLog.WriteException(ex, "RefreshCustomProfiles");
        }
    }

    private void ShowCustomProfileStatus(string message)
    {
        CustomProfileStatusText.Text = message;
        CustomProfileStatusText.Visibility = Visibility.Visible;
        Log(message);
    }

    private void ApplyLoadedProfile(AppSettings loaded, string sourceLabel)
    {
        // Keep this machine's connection identity; only adopt the profile's tuning/bindings/output.
        _settings.CopyFrom(loaded, includeConnectionState: false);
        SyncUiFromSettings();
        SaveSettingsFromUi();
        ShowCustomProfileStatus($"Loaded profile: {sourceLabel}");
    }

    private void ResetDefaultsButton_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            this,
            "Reset all sliders, bindings, and output settings to their defaults? Your paired board and saved profiles are kept.",
            "Reset to defaults",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            // Adopt a fresh default settings snapshot while keeping this machine's connection identity.
            ApplyLoadedProfile(new AppSettings(), "defaults");
        }
        catch (Exception ex)
        {
            _fileLog.WriteException(ex, "ResetDefaults");
            ShowCustomProfileStatus($"Could not reset: {ex.Message}");
        }
    }

    private void SaveProfileButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SaveSettingsFromUi();
            var dialog = new NamePromptDialog(
                "Save profile",
                "Name this profile:",
                confirmLabel: "Save",
                validate: name => _settingsStore.ProfileExists(name)
                    ? "A profile with that name already exists — use Update to overwrite it, or pick another name."
                    : null)
            {
                Owner = this,
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            var name = dialog.ResponseText;
            _settingsStore.SaveProfile(name, _settings);
            RefreshCustomProfiles(SettingsStore.SanitizeProfileName(name));
            ShowCustomProfileStatus($"Saved profile: {name}");
        }
        catch (Exception ex)
        {
            _fileLog.WriteException(ex, "SaveProfile");
            ShowCustomProfileStatus($"Could not save profile: {ex.Message}");
        }
    }

    private void UpdateProfileButton_Click(object sender, RoutedEventArgs e)
    {
        if (CustomProfileCombo.SelectedItem is not string name)
        {
            ShowCustomProfileStatus("Select a profile to update.");
            return;
        }

        try
        {
            SaveSettingsFromUi();
            _settingsStore.SaveProfile(name, _settings);
            ShowCustomProfileStatus($"Updated profile: {name}");
        }
        catch (Exception ex)
        {
            _fileLog.WriteException(ex, "UpdateProfile");
            ShowCustomProfileStatus($"Could not update profile: {ex.Message}");
        }
    }

    private void LoadProfileButton_Click(object sender, RoutedEventArgs e)
    {
        if (CustomProfileCombo.SelectedItem is not string name)
        {
            ShowCustomProfileStatus("Select a profile to load.");
            return;
        }

        try
        {
            var loaded = _settingsStore.LoadProfile(name);
            if (loaded is null)
            {
                ShowCustomProfileStatus($"Could not read profile: {name}");
                RefreshCustomProfiles();
                return;
            }

            ApplyLoadedProfile(loaded, name);
        }
        catch (Exception ex)
        {
            _fileLog.WriteException(ex, "LoadProfile");
            ShowCustomProfileStatus($"Could not load profile: {ex.Message}");
        }
    }

    private void DeleteProfileButton_Click(object sender, RoutedEventArgs e)
    {
        if (CustomProfileCombo.SelectedItem is not string name)
        {
            ShowCustomProfileStatus("Select a profile to delete.");
            return;
        }

        var confirm = MessageBox.Show(
            this,
            $"Delete the profile \"{name}\"? This cannot be undone.",
            "Delete profile",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            _settingsStore.DeleteProfile(name);
            RefreshCustomProfiles();
            ShowCustomProfileStatus($"Deleted profile: {name}");
        }
        catch (Exception ex)
        {
            _fileLog.WriteException(ex, "DeleteProfile");
            ShowCustomProfileStatus($"Could not delete profile: {ex.Message}");
        }
    }

    private void ExportProfileButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SaveSettingsFromUi();
            var suggested = CustomProfileCombo.SelectedItem as string ?? _settings.ActiveProfileName;
            var dialog = new SaveFileDialog
            {
                Title = "Export settings",
                Filter = "Balance Board profile (*.json)|*.json|All files (*.*)|*.*",
                FileName = $"{SettingsStore.SanitizeProfileName(suggested)}.bbprofile.json",
                DefaultExt = ".json",
                AddExtension = true,
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            _settingsStore.ExportSettings(_settings, dialog.FileName);
            ShowCustomProfileStatus($"Exported settings to {dialog.FileName}");
        }
        catch (Exception ex)
        {
            _fileLog.WriteException(ex, "ExportProfile");
            ShowCustomProfileStatus($"Could not export: {ex.Message}");
        }
    }

    private void ImportProfileButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new OpenFileDialog
            {
                Title = "Import settings",
                Filter = "Balance Board profile (*.json)|*.json|All files (*.*)|*.*",
                CheckFileExists = true,
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            var imported = _settingsStore.ImportSettings(dialog.FileName);
            if (imported is null)
            {
                ShowCustomProfileStatus("That file is not a valid settings profile.");
                return;
            }

            var suggestedName = Path.GetFileNameWithoutExtension(dialog.FileName)
                .Replace(".bbprofile", string.Empty, StringComparison.OrdinalIgnoreCase);
            var namePrompt = new NamePromptDialog(
                "Import profile",
                "Save the imported settings under this profile name:",
                initialValue: suggestedName,
                confirmLabel: "Import")
            {
                Owner = this,
            };

            var label = string.IsNullOrWhiteSpace(suggestedName) ? "imported file" : suggestedName;
            if (namePrompt.ShowDialog() == true)
            {
                _settingsStore.SaveProfile(namePrompt.ResponseText, imported);
                RefreshCustomProfiles(SettingsStore.SanitizeProfileName(namePrompt.ResponseText));
                label = namePrompt.ResponseText;
            }

            ApplyLoadedProfile(imported, label);
        }
        catch (Exception ex)
        {
            _fileLog.WriteException(ex, "ImportProfile");
            ShowCustomProfileStatus($"Could not import: {ex.Message}");
        }
    }

    private void SyncUiFromSettings()
    {
        _suppressSettingEvents = true;
        _settingsSync.ApplyToUi();
        RefreshVJoyDeviceCombo();
        BindBoardButton.IncludeVJoyButton = true;
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

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        RequestSaveSettingsFromUi(immediate: true);

    private void ApplySensitivityPreset(SensitivityLevel level)
    {
        SensitivityPresets.Apply(_settings, level);
        _settings.OneFootMode = false;
        _suppressSettingEvents = true;
        OneFootModeCheck.IsChecked = false;
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
    private void SensitivityHairTrigger_Click(object sender, RoutedEventArgs e) => ApplySensitivityPreset(SensitivityLevel.HairTrigger);

    private void AxisLock_Changed(object sender, RoutedEventArgs e)
    {
        if (!_uiReady || _suppressSettingEvents)
        {
            return;
        }

        if (LockLeftRightAxisCheck.IsChecked == true)
        {
            _suppressSettingEvents = true;
            LockForwardBackwardAxisCheck.IsChecked = false;
            _suppressSettingEvents = false;
        }
        else if (LockForwardBackwardAxisCheck.IsChecked == true)
        {
            _suppressSettingEvents = true;
            LockLeftRightAxisCheck.IsChecked = false;
            _suppressSettingEvents = false;
        }

        SaveSettingsFromUi();
    }

    private void SplitAxisSensitivity_Changed(object sender, RoutedEventArgs e)
    {
        if (!_uiReady || _suppressSettingEvents)
        {
            return;
        }

        if (SplitAxisSensitivityCheck.IsChecked == true)
        {
            _suppressSettingEvents = true;
            if (_settings.SensitivityLeftRight is null)
            {
                SensitivityLeftRightSlider.Value = SensitivitySlider.Value;
            }

            if (_settings.SensitivityForwardBackward is null)
            {
                SensitivityForwardBackwardSlider.Value = SensitivitySlider.Value;
            }

            _suppressSettingEvents = false;
        }
        else
        {
            _suppressSettingEvents = true;
            SensitivityLeftRightSlider.Value = 0;
            SensitivityForwardBackwardSlider.Value = 0;
            _suppressSettingEvents = false;
        }

        UpdateSensitivityModeUi();
        UpdateSliderLabels();
        SaveSettingsFromUi();
    }

    private void SplitAxisDeadzone_Changed(object sender, RoutedEventArgs e)
    {
        if (!_uiReady || _suppressSettingEvents)
        {
            return;
        }

        if (SplitAxisDeadzoneCheck.IsChecked == true)
        {
            _suppressSettingEvents = true;
            if (_settings.DeadzoneLeftRightPercent is null)
            {
                DeadzoneLeftRightSlider.Value = DeadzoneSlider.Value;
            }

            if (_settings.DeadzoneForwardBackwardPercent is null)
            {
                DeadzoneForwardBackwardSlider.Value = DeadzoneSlider.Value;
            }

            _suppressSettingEvents = false;
        }

        UpdateSensitivityModeUi();
        UpdateSliderLabels();
        SaveSettingsFromUi();
    }

    private void DetailLevelCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        RequestSaveSettingsFromUi(immediate: true);

    private void ResponseCurveCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        RequestSaveSettingsFromUi(immediate: true);

    private void OneFootMode_Changed(object sender, RoutedEventArgs e)
    {
        if (!_uiReady || _suppressSettingEvents)
        {
            return;
        }

        if (OneFootModeCheck.IsChecked == true)
        {
            OneFootPresets.Apply(_settings);
            _suppressSettingEvents = true;
            SimpleSensitivityCheck.IsChecked = false;
            TriggerLeftRightSlider.Value = _settings.TriggerLeftRight;
            TriggerForwardBackwardSlider.Value = _settings.TriggerForwardBackward;
            DeadzoneSlider.Value = _settings.DeadzonePercent;
            SensitivitySlider.Value = _settings.Sensitivity;
            JumpThresholdSlider.Value = _settings.JumpWeightThresholdKg;
            JumpHoldSlider.Value = _settings.JumpHoldSeconds;
            ResponseCurveCombo.SelectedIndex = (int)_settings.ResponseCurve;
            _suppressSettingEvents = false;
            UpdateJumpPresetButtons();
            UpdateSensitivityPresetButtons();
        }
        else
        {
            OneFootPresets.Clear(_settings);
        }

        SaveSettingsFromUi();
    }

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

    private void ClearLogViewButton_Click(object sender, RoutedEventArgs e)
    {
        LogBox.Clear();
        _lastLogLine = string.Empty;
        UpdateLogPreview();
    }

    private void ShowLogButton_Click(object sender, RoutedEventArgs e)
    {
        SessionLogExpander.IsExpanded = !SessionLogExpander.IsExpanded;
    }

    private void SessionLogExpander_Changed(object sender, RoutedEventArgs e)
    {
        UpdateSessionLogUi();
        SaveSettingsFromUi();
    }

    private void UpdateSessionLogUi()
    {
        var expanded = SessionLogExpander.IsExpanded;
        ShowLogButton.Content = expanded ? "Hide log" : "Show log";
        LogPreviewText.Visibility = expanded || string.IsNullOrEmpty(_lastLogLine)
            ? Visibility.Collapsed
            : Visibility.Visible;
        UpdateLogPreview();
    }

    private void UpdateLogPreview()
    {
        LogPreviewText.Text = string.IsNullOrEmpty(_lastLogLine) ? string.Empty : $"— {_lastLogLine}";
    }

    public void ForceShutdown()
    {
        if (_shutdownCompleted)
        {
            return;
        }

        _shutdownCompleted = true;
        _autoExitCts?.Cancel();
        _autoExitCts?.Dispose();
        _autoExitCts = null;
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

        _physicalTestRunner?.FinishIfNeeded();
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

    internal SettingsStore TestSettingsStore => _settingsStore;

    internal AppSettings TestSettings => _settings;

    internal BalanceBoardSession TestSession => _session;

    internal FileLogService TestFileLog => _fileLog;

    internal TabControl TestMainTabControl => MainTabControl;

    internal TabItem TestAdvancedTab => AdvancedTab;

    internal TabItem TestFineTuningTab => FineTuningTab;

    internal ComboBox TestProfileCombo => ProfileCombo;

    internal ComboBox TestDetailLevelCombo => DetailLevelCombo;

    internal ComboBox TestThemeCombo => ThemeCombo;

    internal Slider TestDeadzoneSlider => DeadzoneSlider;

    internal Slider TestPollIntervalSlider => PollIntervalSlider;

    internal CheckBox TestAutoConnectCheck => AutoConnectCheck;

    internal CheckBox TestStartMinimizedCheck => StartMinimizedCheck;

    internal TextBlock TestConnectionChipText => ConnectionChipText;

    internal TextBox TestLogBox => LogBox;

    internal Visibility TestPhysicalTestPanelVisibility => PhysicalTestPanel.Visibility;

    internal string TestPhysicalTestScenarioText => PhysicalTestScenarioText.Text;

    internal string TestPhysicalTestStepTitle => PhysicalTestStepTitleText.Text;

    internal void TestSelectTab(int index) => MainTabControl.SelectedIndex = index;

    internal void TestClickGamePreset() => GamePreset_Click(GamePresetButton, new RoutedEventArgs());

    internal void TestClickMinecraftPreset() => MinecraftPreset_Click(MinecraftPresetButton, new RoutedEventArgs());

    internal void TestClickDesktopPreset() => KeyboardPreset_Click(DesktopPresetButton, new RoutedEventArgs());

    internal void TestRunHealthCheck() => RunHealthCheckButton_Click(RunHealthCheckButton, new RoutedEventArgs());

    internal ComboBox TestCustomProfileCombo => CustomProfileCombo;

    internal void TestSaveCustomProfile(string name)
    {
        SaveSettingsFromUi();
        _settingsStore.SaveProfile(name, _settings);
        RefreshCustomProfiles(SettingsStore.SanitizeProfileName(name));
    }

    internal void TestResetDefaults() => ApplyLoadedProfile(new AppSettings(), "defaults");

    internal bool TestLoadCustomProfile(string name)
    {
        var loaded = _settingsStore.LoadProfile(name);
        if (loaded is null)
        {
            return false;
        }

        ApplyLoadedProfile(loaded, name);
        return true;
    }

    internal void TestPumpDispatcher(TimeSpan? timeout = null)
    {
        var frame = new DispatcherFrame();
        _ = Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
        var debounceDeadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(SettingsSaveDebounceMs + 50);
        while (DateTime.UtcNow < debounceDeadline)
        {
            var debounceFrame = new DispatcherFrame();
            _ = Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => debounceFrame.Continue = false));
            Dispatcher.PushFrame(debounceFrame);
        }
    }
}
