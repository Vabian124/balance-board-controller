using BalanceBoard.Core.Abstractions;
using BalanceBoard.Core.Models;

namespace BalanceBoard.Core.Services;

public sealed class BalanceBoardSession : IDisposable
{
    private readonly ConnectionWorker _worker;
    private readonly bool _ownsWorker;
    private readonly IBalanceBoardConnection _connection;
    private readonly IBluetoothPairingService _pairing;
    private readonly BalanceProcessor _processor = new();
    private readonly IGameControllerOutput _vjoy;
    private readonly IActionSimulator _input;
    private bool _disposed;
    private CancellationTokenSource? _connectCts;
    private CancellationTokenSource? _recoveryCts;
    private Task? _recoveryTask;
    private bool _loggedFirstPoll;
    private bool _wasJumping;
    private string? _lastSettingsLogKey;
    private bool _manualDisconnect;
    private bool _adapterMacChanged;
    private DateTime? _connectedAtUtc;
    private DateTime? _lastReadingUtc;
    private ConnectionPhase _connectionPhase = ConnectionPhase.Offline;
    private volatile bool _staleHidHandled;
    private int _connectActive;
    private int _pollGate;

    private bool _adapterMacConfirmedAtConnectStart;

    private bool UsesSimulatedConnection => _connection is SimulatedBalanceBoardConnection;

    public void CancelConnect() => _connectCts?.Cancel();

    public event Action<ProcessedBalance>? Processed;
    public event Action<string>? Log;
    public event Action<string>? StatusChanged;
    public event Action<ConnectionPhase>? ConnectionPhaseChanged;

    public ConnectionPhase ConnectionPhase =>
        _worker.TryInvoke(() => _connectionPhase, out var phase, ConnectionPhase.Offline)
            ? phase
            : ConnectionPhase.Offline;

    public bool IsConnected =>
        _worker.TryInvoke(() => IsSessionHealthy(), out var healthy, false) && healthy;

    public string? ConnectedDeviceId
    {
        get
        {
            _worker.TryInvoke(() => _connection.ConnectedDeviceId, out var deviceId);
            return deviceId;
        }
    }
    public AppSettings Settings { get; private set; } = new();

    public BalanceBoardSession(
        IGameControllerOutput? gameController = null,
        IActionSimulator? actionSimulator = null,
        IBalanceBoardConnection? connection = null,
        IBluetoothPairingService? pairing = null,
        ConnectionWorker? worker = null)
    {
        _ownsWorker = worker is null;
        _worker = worker ?? new ConnectionWorker();
        _connection = connection ?? new BalanceBoardConnection();
        _pairing = pairing ?? new BluetoothPairingService();
        _vjoy = gameController ?? new VJoyController();
        _input = actionSimulator ?? new InputSimulator();

        _connection.StatusChanged += msg => SafeCallbacks.Raise(StatusChanged, msg);
        _connection.ConnectLog += msg => SafeCallbacks.Raise(Log, msg);
        _connection.Error += msg => SafeCallbacks.Raise(Log, $"Error: {msg}");
        _connection.ReadingAvailable += OnReadingAvailable;
        if (_vjoy is VJoyController vjoyController)
        {
            vjoyController.Log += msg => SafeCallbacks.Raise(Log, msg);
        }

        _worker.SetPollTick(Poll);
    }

    public void LoadSettings(AppSettings settings, bool initializeVJoy = true)
    {
        Settings = settings;
        _worker.PollIntervalMs = settings.PollIntervalMs;
        var key =
            $"{settings.ActiveProfileName}|{settings.EnableVJoy}|{settings.JumpWeightThresholdKg:0.0}|{settings.UiDetailLevel}";
        if (!string.Equals(key, _lastSettingsLogKey, StringComparison.Ordinal))
        {
            _lastSettingsLogKey = key;
            Log?.Invoke(
                $"[SETTINGS] Profile={settings.ActiveProfileName} " +
                $"vJoy={settings.EnableVJoy} jumpThreshold={settings.JumpWeightThresholdKg:0.0}kg " +
                $"detail={settings.UiDetailLevel}");
        }

        if (!initializeVJoy)
        {
            return;
        }

        SyncVJoyFromSettingsThreadSafe();
    }

    /// <summary>
    /// Marshals vJoy init/shutdown onto the ConnectionWorker thread. Poll() calls
    /// _vjoy.Update()/Center() inline on that same thread — without this, a settings
    /// save from the UI thread (e.g. toggling "Enable vJoy" or changing the device id)
    /// could Shutdown()/Initialize() the vJoy device concurrently with an in-flight
    /// Update() call on the worker thread.
    /// </summary>
    private void SyncVJoyFromSettingsThreadSafe() => _worker.Invoke(SyncVJoyFromSettings);

    private void SyncVJoyFromSettings()
    {
        if (Settings.EnableVJoy)
        {
            _vjoy.Initialize(Settings.VJoyDeviceId);
        }
        else
        {
            _vjoy.Shutdown();
        }
    }

    public IReadOnlyList<string> DiscoverDevices()
    {
        _worker.TryInvoke(_connection.DiscoverDeviceIds, out var ids, Array.Empty<string>());
        return ids ?? Array.Empty<string>();
    }

    public bool Connect(int deviceIndex = 0, string? preferredDeviceId = null) =>
        _worker.TryInvoke(() =>
        {
            if (!_connection.Connect(deviceIndex, preferredDeviceId))
            {
                return false;
            }

            OnConnected();
            return true;
        }, out var ok, false) && ok;

    public bool IsBoardVisible() => DiscoverDevices().Count > 0;

    public async Task<ConnectResult> ConnectWithIntentAsync(
        ConnectionIntent intent,
        int deviceIndex = 0,
        int discoveryRounds = 4,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ConnectResult.Fail(ConnectStatus.Cancelled);
        }

        if (Interlocked.CompareExchange(ref _connectActive, 1, 0) != 0)
        {
            Log?.Invoke("[CONNECT] Connect already in progress — ignoring duplicate request.");
            return ConnectResult.Fail(ConnectStatus.AlreadyInProgress);
        }

        try
        {
            CancelInFlightRecoveryHandoff();
            return await Task.Run(() =>
                _worker.InvokeStrict(() => ConnectWithIntentCore(intent, deviceIndex, discoveryRounds, cancellationToken)));
        }
        catch (OperationCanceledException)
        {
            return ConnectResult.Fail(ConnectStatus.Cancelled);
        }
        finally
        {
            Interlocked.Exchange(ref _connectActive, 0);
        }
    }

    /// <summary>
    /// QuickReconnect: HID only (boot / second-instance). PairAndConnect: pairing when user asks or --connect.
    /// </summary>
    public ConnectResult ConnectWithIntent(
        ConnectionIntent intent,
        int deviceIndex = 0,
        int discoveryRounds = 4,
        CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _connectActive, 1, 0) != 0)
        {
            Log?.Invoke("[CONNECT] Connect already in progress — ignoring duplicate request.");
            return ConnectResult.Fail(ConnectStatus.AlreadyInProgress);
        }

        try
        {
            CancelInFlightRecoveryHandoff();
            return _worker.InvokeStrict(() => ConnectWithIntentCore(intent, deviceIndex, discoveryRounds, cancellationToken));
        }
        finally
        {
            Interlocked.Exchange(ref _connectActive, 0);
        }
    }

    /// <summary>
    /// Legacy entry point — full pairing flow.
    /// </summary>
    public bool ConnectOrPair(int deviceIndex = 0, int discoveryRounds = 4, CancellationToken cancellationToken = default) =>
        ConnectWithIntent(ConnectionIntent.PairAndConnect, deviceIndex, discoveryRounds, cancellationToken).IsSuccess;

    /// <summary>
    /// Signals cancellation to any in-flight background Bluetooth recovery attempt before
    /// this connect's action is queued onto the ConnectionWorker. Without this, a manual
    /// or quick connect issued while BluetoothRecoveryLoop is mid-attempt (e.g. a slow
    /// pairing round) would simply queue behind it and have to wait for that attempt to run
    /// to completion — the recovery loop only observes cancellation at its own checkpoints,
    /// and those checkpoints are never reached until something cancels the token. Cancelling
    /// here (from the caller's thread, before the worker even starts this connect) lets an
    /// in-flight recovery step unwind at its very next check instead of always finishing
    /// first. StopRecovery() still runs once this connect's action reaches the worker
    /// thread, for the remaining teardown/bookkeeping.
    /// </summary>
    private void CancelInFlightRecoveryHandoff()
    {
        try
        {
            _recoveryCts?.Cancel();
        }
        catch
        {
            // Best-effort — StopRecovery() retries teardown once this connect runs.
        }
    }

    private ConnectResult ConnectWithIntentCore(
        ConnectionIntent intent,
        int deviceIndex,
        int discoveryRounds,
        CancellationToken cancellationToken)
    {
        if (_connection.IsConnected)
        {
            if (IsSessionHealthy())
            {
                Log?.Invoke("[CONNECT] Already connected — ignoring duplicate request.");
                return ConnectResult.Ok();
            }

            if (_connectedAtUtc is not null
                && (DateTime.UtcNow - _connectedAtUtc.Value).TotalMilliseconds
                    <= BalanceConstants.ConnectHealthGraceMs)
            {
                Log?.Invoke("[CONNECT] HID session opening — ignoring duplicate request.");
                return ConnectResult.Fail(ConnectStatus.AlreadyInProgress);
            }
        }

        StopRecovery();
        _manualDisconnect = false;
        _adapterMacChanged = false;
        _adapterMacConfirmedAtConnectStart = false;
        LogBluetoothAdapterState();
        ConnectionFlowLogger.LogIntent(Log, intent);

        _connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            var ct = _connectCts.Token;
            // Probe Bluetooth before WiimoteLib HID — HID enumeration can spuriously break InTheHand.
            if (!UsesSimulatedConnection && !WaitForBluetoothAtConnectStart(ct))
            {
                if (ct.IsCancellationRequested)
                {
                    Log?.Invoke("Connection cancelled.");
                    StatusChanged?.Invoke("Connection cancelled.");
                    ConnectionFlowLogger.LogFlowComplete(Log, false);
                    return ConnectResult.Fail(ConnectStatus.Cancelled);
                }

                StatusChanged?.Invoke("Bluetooth is off — turn it on, then click Connect.");
                ConnectionFlowLogger.LogFlowComplete(Log, false);
                return ConnectResult.Fail(ConnectStatus.BluetoothUnavailable);
            }

            ConnectionFlowLogger.LogHidDiscovery(Log, _connection.DiscoverDeviceIds());

            var preferredDeviceId = ResolvePreferredDeviceId();

            if (intent == ConnectionIntent.QuickReconnect)
            {
                ConnectResult quick;
                if (_adapterMacChanged)
                {
                    Log?.Invoke("[CONNECT] Adapter address changed — escalating to full pairing (press SYNC if needed).");
                    quick = TryPairAndConnect(deviceIndex, discoveryRounds, ct);
                }
                else
                {
                    quick = TryQuickReconnect(deviceIndex, preferredDeviceId, ct);
                }

                if (quick.IsSuccess)
                {
                    PersistAdapterMacIfKnown();
                }

                ConnectionFlowLogger.LogFlowComplete(Log, quick.IsSuccess);
                return quick;
            }

            if (_connection.DiscoverDeviceIds().Count == 0)
            {
                _pairing.WakePairedDevices(Log);
                ct.ThrowIfCancellationRequested();
                EnsureBluetoothReady();
            }

            if (TryConnect(deviceIndex, preferredDeviceId))
            {
                ConnectionFlowLogger.LogFlowComplete(Log, true);
                return ConnectResult.Ok();
            }

            var result = TryPairAndConnect(deviceIndex, discoveryRounds, ct);

            if (result.IsSuccess)
            {
                PersistAdapterMacIfKnown();
            }

            ConnectionFlowLogger.LogFlowComplete(Log, result.IsSuccess);
            return result;
        }
        catch (OperationCanceledException)
        {
            Log?.Invoke("Connection cancelled.");
            StatusChanged?.Invoke("Connection cancelled.");
            ConnectionFlowLogger.LogFlowComplete(Log, false);
            return ConnectResult.Fail(ConnectStatus.Cancelled);
        }
        catch (Exception ex)
        {
            Log?.Invoke($"Connection error: {ex.Message}");
            Log?.Invoke(ex.StackTrace ?? string.Empty);
            StatusChanged?.Invoke("Connection error — see log.");
            ConnectionFlowLogger.LogFlowComplete(Log, false);
            return ConnectResult.Fail(ConnectStatus.Error, ex.Message);
        }
        finally
        {
            _connectCts?.Dispose();
            _connectCts = null;
        }
    }

    private bool TryConnect(int deviceIndex, string? preferredDeviceId = null)
    {
        if (!_connection.Connect(deviceIndex, preferredDeviceId))
        {
            return false;
        }

        OnConnected();
        PersistAdapterMacIfKnown();
        return true;
    }

    private ConnectResult TryQuickReconnect(int deviceIndex, string? preferredDeviceId, CancellationToken ct)
    {
        var visible = _connection.DiscoverDeviceIds();
        if (visible.Count > 0)
        {
            Log?.Invoke("[CONNECT] HID reconnect: board visible — opening session (WiiBalanceWalker fast path).");
            StatusChanged?.Invoke("Finding board…");
            return TryConnectWithHidRetries(deviceIndex, preferredDeviceId, ct);
        }

        Log?.Invoke("[CONNECT] HID reconnect: wake probe then open preferred device.");
        StatusChanged?.Invoke("Finding board…");
        _pairing.WakePairedDevices(Log);
        ct.ThrowIfCancellationRequested();

        for (var attempt = 1; attempt <= BalanceConstants.PostPairHidRetryAttempts; attempt++)
        {
            if (TryConnect(deviceIndex, preferredDeviceId))
            {
                return ConnectResult.Ok();
            }

            if (attempt >= BalanceConstants.PostPairHidRetryAttempts)
            {
                break;
            }

            Log?.Invoke($"[CONNECT] HID reconnect: retry {attempt}/{BalanceConstants.PostPairHidRetryAttempts - 1}…");
            StatusChanged?.Invoke("Finding board…");
            if (!ct.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(BalanceConstants.PostPairHidRetryMs)))
            {
                // waited
            }

            ct.ThrowIfCancellationRequested();
            _pairing.WakePairedDevices(Log);
            ct.ThrowIfCancellationRequested();
        }

        StatusChanged?.Invoke("Board not found — trying again soon.");
        DebugSessionTrace.Write(
            "BalanceBoardSession.cs:TryQuickReconnect",
            "quick reconnect failed",
            "H2",
            new { preferredDeviceId, hidVisible = _connection.DiscoverDeviceIds().Count });
        return ConnectResult.Fail(ConnectStatus.NoDevices);
    }

    private ConnectResult TryConnectWithHidRetries(
        int deviceIndex,
        string? preferredDeviceId,
        CancellationToken ct)
    {
        for (var attempt = 1; attempt <= BalanceConstants.PostPairHidRetryAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            if (TryConnect(deviceIndex, preferredDeviceId))
            {
                return ConnectResult.Ok();
            }

            if (attempt < BalanceConstants.PostPairHidRetryAttempts)
            {
                Log?.Invoke(
                    $"[CONNECT] HID not ready yet — retry {attempt}/{BalanceConstants.PostPairHidRetryAttempts} " +
                    $"in {BalanceConstants.PostPairHidRetryMs} ms (board may still be waking).");
                if (ct.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(BalanceConstants.PostPairHidRetryMs)))
                {
                    ct.ThrowIfCancellationRequested();
                }
            }
        }

        DebugSessionTrace.Write(
            "BalanceBoardSession.cs:TryConnectWithHidRetries",
            "hid retries exhausted",
            "H3",
            new { preferredDeviceId, hidVisible = _connection.DiscoverDeviceIds().Count });
        return ConnectResult.Fail(ConnectStatus.HidFailed, "Paired but HID connect failed.");
    }

    private ConnectResult TryWakeAndConnect(
        int deviceIndex,
        string? preferredDeviceId,
        CancellationToken ct,
        bool wakeFirst = true)
    {
        if (wakeFirst)
        {
            _pairing.WakePairedDevices(Log);
            ct.ThrowIfCancellationRequested();
        }
        else
        {
            Log?.Invoke("[CONNECT] Skipping wake probe — board was just paired.");
        }

        return TryConnectWithHidRetries(deviceIndex, preferredDeviceId, ct);
    }

    /// <summary>After Bluetooth pairing: wait for HID, brief wake ping, then open session (WiiBalanceWalker FormBluetooth).</summary>
    private ConnectResult TryHidAfterBluetoothPair(int deviceIndex, string? preferredDeviceId, CancellationToken ct)
    {
        Log?.Invoke("[CONNECT] Post-pair: waiting for Windows HID enumeration…");
        var deadline = DateTime.UtcNow.AddMilliseconds(BalanceConstants.PostPairHidEnumerateMs);
        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            if (_connection.DiscoverDeviceIds().Count > 0)
            {
                Log?.Invoke("[CONNECT] Post-pair: Wii HID device(s) visible.");
                break;
            }

            if (ct.WaitHandle.WaitOne(BalanceConstants.PostPairHidRetryMs))
            {
                ct.ThrowIfCancellationRequested();
            }
        }

        ct.ThrowIfCancellationRequested();
        return TryConnectWithHidRetries(deviceIndex, preferredDeviceId, ct);
    }

    private ConnectResult TryPairAndConnect(int deviceIndex, int discoveryRounds, CancellationToken ct)
    {
        Log?.Invoke("[CONNECT] Pair-and-connect: automatic Bluetooth pairing with permanent host PIN.");
        StatusChanged?.Invoke("Finding board…");
        var wakeResult = _pairing.PairDiscoverableBoard(Log, ct, removeStalePairings: false);
        if (wakeResult.Success)
        {
            ct.ThrowIfCancellationRequested();
            var connected = TryHidAfterBluetoothPair(deviceIndex, ResolvePreferredDeviceId(), ct);
            if (connected.IsSuccess)
            {
                return ConnectResult.Ok(wakeResult.Message);
            }

            return connected;
        }

        if (!string.IsNullOrWhiteSpace(wakeResult.Message))
        {
            Log?.Invoke(wakeResult.Message);
        }

        for (var round = 1; round <= discoveryRounds; round++)
        {
            ct.ThrowIfCancellationRequested();
            if (!UsesSimulatedConnection && !WaitForBluetoothAtConnectStart(ct))
            {
                if (ct.IsCancellationRequested)
                {
                    return ConnectResult.Fail(ConnectStatus.Cancelled);
                }

                return ConnectResult.Fail(ConnectStatus.BluetoothUnavailable);
            }

            ConnectionFlowLogger.LogPairingRound(Log, round, discoveryRounds, removeStale: round == 1);

            if (round == 1)
            {
                Log?.Invoke("Starting automatic Bluetooth pairing. Press the red SYNC button.");
            }
            else
            {
                Log?.Invoke($"Still searching… press SYNC again (round {round}/{discoveryRounds}).");
            }

            var pairResult = _pairing.PairDiscoverableBoard(
                Log,
                ct,
                removeStalePairings: round == 1);
            Log?.Invoke(pairResult.Message);

            if (!pairResult.Success)
            {
                if (round < discoveryRounds && !ct.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(BalanceConstants.PairRoundDelayMs)))
                {
                    // wait between rounds
                }

                ct.ThrowIfCancellationRequested();
                continue;
            }

            ct.ThrowIfCancellationRequested();
            var roundConnect = TryHidAfterBluetoothPair(deviceIndex, ResolvePreferredDeviceId(), ct);
            if (roundConnect.IsSuccess)
            {
                return ConnectResult.Ok(pairResult.Message);
            }
        }

        StatusChanged?.Invoke("Could not find the board — press SYNC under the battery cover, then Connect.");
        return ConnectResult.Fail(ConnectStatus.PairingFailed);
    }

    private void OnConnected()
    {
        _loggedFirstPoll = false;
        _staleHidHandled = false;
        _manualDisconnect = false;
        _connectedAtUtc = DateTime.UtcNow;
        _lastReadingUtc = null;
        EndRecovery();
        if (_connection is BalanceBoardConnection)
        {
            Log?.Invoke("Starting balance event stream with health poll.");
        }
        else
        {
            Log?.Invoke("Starting balance poll loop.");
        }

        _worker.StartPolling();
        ProbeInitialHealth();

        if (Settings.AutoTareOnConnect)
        {
            TareCore();
        }

        SyncVJoyFromSettings();
    }

    private void ProbeInitialHealth()
    {
        var reading = _connection.GetCurrentReading();
        if (reading?.IsBalanceBoard == true)
        {
            _lastReadingUtc = DateTime.UtcNow;
            SetConnectionPhase(ConnectionPhase.Connected);
            SafeCallbacks.Raise(StatusChanged, "Connected!");
            return;
        }

        SetConnectionPhase(ConnectionPhase.Connecting);
        SafeCallbacks.Raise(StatusChanged, "Finding board…");
    }

    private bool IsSessionHealthy()
    {
        if (!_connection.IsConnected || _lastReadingUtc is null)
        {
            return false;
        }

        return (DateTime.UtcNow - _lastReadingUtc.Value).TotalMilliseconds
            <= BalanceConstants.ReadingHealthTimeoutMs;
    }

    private string? ResolvePreferredDeviceId() =>
        DeviceIdRules.ShouldPersistConnectionState(Settings.LastConnectedDeviceId)
            ? Settings.LastConnectedDeviceId
            : null;

    private void LogBluetoothAdapterState()
    {
        var currentMac = _pairing.TryGetLocalAdapterMac();
        _adapterMacConfirmedAtConnectStart = currentMac is not null;
        if (currentMac is null)
        {
            Log?.Invoke("[CONNECT] Bluetooth adapter MAC unavailable (no radio or driver error).");
            return;
        }

        var display = WiiBluetoothPin.FormatMacForDisplay(currentMac);
        Log?.Invoke($"[CONNECT] Bluetooth adapter {display}.");

        var storedMac = Settings.LastBluetoothAdapterMac;
        if (string.IsNullOrWhiteSpace(storedMac))
        {
            Log?.Invoke("[CONNECT] No saved adapter MAC yet — will store after a successful pair.");
            return;
        }

        if (string.Equals(storedMac, currentMac, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _adapterMacChanged = true;
        var storedDisplay = WiiBluetoothPin.FormatMacForDisplay(storedMac);
        Log?.Invoke(
            $"[CONNECT] Adapter address changed ({storedDisplay} → {display}) — Wii permanent PIN pairing may fail until you re-pair with SYNC.");
    }

    private void PersistAdapterMacIfKnown()
    {
        var currentMac = _pairing.TryGetLocalAdapterMac();
        if (string.IsNullOrWhiteSpace(currentMac))
        {
            return;
        }

        Settings.LastBluetoothAdapterMac = currentMac;
    }

    private void SetConnectionPhase(ConnectionPhase phase)
    {
        if (_connectionPhase == phase)
        {
            return;
        }

        _connectionPhase = phase;
        SafeCallbacks.Raise(ConnectionPhaseChanged, phase);
    }

    public void Disconnect()
    {
        _manualDisconnect = true;
        StopRecovery();
        _worker.Invoke(DisconnectCore);
    }

    private void DisconnectCore()
    {
        Log?.Invoke("[DISCONNECT] Stopping poll loop and releasing outputs.");
        _loggedFirstPoll = false;
        _wasJumping = false;
        _connectedAtUtc = null;
        _lastReadingUtc = null;
        _staleHidHandled = false;
        _worker.StopPolling();
        _input.ReleaseAll();
        _vjoy.Center();
        _vjoy.Shutdown();
        _connection.Disconnect();
        SetConnectionPhase(ConnectionPhase.Offline);
        StatusChanged?.Invoke("Disconnected.");
    }

    public void Tare() => _worker.Invoke(TareCore);

    private void TareCore()
    {
        _connection.Tare();
        _processor.Tare();
        Log?.Invoke("Balance board tared.");
    }

    public void SetCenter() => _worker.Invoke(() =>
    {
        var reading = _connection.GetCurrentReading();
        if (reading is null)
        {
            return;
        }

        _processor.SetCenterFromCurrentReading(reading);
        Log?.Invoke("Current balance set as center.");
    });

    public void ResetCenter()
    {
        _processor.ResetCenterOffsets();
        Log?.Invoke("Center offset reset.");
    }

    public bool CanSetCenter() =>
        _worker.TryInvoke(() =>
        {
            var reading = _connection.GetCurrentReading();
            return reading is not null && _processor.CanSetCenter(reading.WeightKg);
        }, out var ok, false) && ok;

    public bool CanResetCenter() =>
        _worker.TryInvoke(() =>
        {
            var reading = _connection.GetCurrentReading();
            return reading is not null && _processor.CanResetCenter(reading.WeightKg);
        }, out var ok, false) && ok;

    public void ApplyControllerPreset() =>
        ApplyPreset(ActionPresets.ApplyGameController, "Applied game controller preset (vJoy X/Y from balance).");

    public void ApplyPedalPreset() =>
        ApplyPreset(ActionPresets.ApplyPedal, "Applied pedal preset (vJoy Z/RX/RY/RZ from load sensors).");

    public void ApplyKeyboardPreset() =>
        ApplyPreset(ActionPresets.ApplyKeyboardMovement, "Applied hand-free desktop preset (WASD + Shift + jump click).");

    public void ApplyMousePreset() =>
        ApplyPreset(ActionPresets.ApplyBalanceMouse, "Applied balance mouse preset (lean to move, jump to click).");

    public void ApplyMinecraftPreset() =>
        ApplyPreset(ActionPresets.ApplyMinecraft, "Applied Minecraft (Controlify) preset — lean = move, jump = vJoy A.");

    public void ApplyProfile(string profileName)
    {
        ActionPresets.Apply(Settings, profileName);
        SyncVJoyFromSettingsThreadSafe();
        Log?.Invoke($"Applied profile: {profileName}");
    }

    private void ApplyPreset(Action<AppSettings> apply, string logMessage)
    {
        apply(Settings);
        SyncVJoyFromSettingsThreadSafe();
        Log?.Invoke(logMessage);
    }

    private void OnReadingAvailable()
    {
        if (!_disposed)
        {
            try
            {
                Poll();
            }
            catch (Exception ex)
            {
                Log?.Invoke($"Reading callback error: {ex.Message}");
                Log?.Invoke(ex.StackTrace ?? string.Empty);
            }
        }
    }

    private void Poll()
    {
        if (_disposed)
        {
            return;
        }

        // Poll() is reachable from two different threads for a real device: the
        // ConnectionWorker's own idle-tick health poll (SetPollTick) and WiimoteLib's
        // internal HID read thread firing OnReadingAvailable on every incoming report.
        // Neither BalanceProcessor's tare/jump state nor ActionEngine's pressed-key state
        // is synchronized, so two overlapping calls would race those fields. Skip an
        // overlapping call rather than block WiimoteLib's callback thread — the next tick
        // or report picks up the current reading a moment later.
        if (Interlocked.CompareExchange(ref _pollGate, 1, 0) != 0)
        {
            return;
        }

        try
        {
            if (_connection.IsConnected && !IsSessionHealthy())
            {
                if (_lastReadingUtc is null
                    && _connectedAtUtc is not null
                    && (DateTime.UtcNow - _connectedAtUtc.Value).TotalMilliseconds
                        <= BalanceConstants.ConnectHealthGraceMs)
                {
                    // Still waiting for the first balance reading after HID open.
                }
                else
                {
                    HandleStaleHidSession();
                    return;
                }
            }

            var hadLiveSession = _loggedFirstPoll || _lastReadingUtc is not null;
            var reading = _connection.GetCurrentReading();
            if (reading is not null)
            {
                OnReading(reading);
                return;
            }

            if (hadLiveSession && !_connection.IsConnected)
            {
                HandleUnexpectedDisconnect();
            }
        }
        catch (Exception ex)
        {
            Log?.Invoke($"Poll error: {ex.Message}");
            Log?.Invoke(ex.StackTrace ?? string.Empty);
        }
        finally
        {
            Interlocked.Exchange(ref _pollGate, 0);
        }
    }

    private void HandleStaleHidSession()
    {
        if (_staleHidHandled || _manualDisconnect)
        {
            return;
        }

        _staleHidHandled = true;
        _loggedFirstPoll = false;
        Log?.Invoke("[DISCONNECT] HID session stale — no balance readings (board may be flashing).");
        SafeCallbacks.Raise(StatusChanged, "Trying again…");

        try
        {
            _input.ReleaseAll();
        }
        catch (Exception ex)
        {
            Log?.Invoke($"Release inputs after stale HID: {ex.Message}");
        }

        try
        {
            _vjoy.Center();
        }
        catch (Exception ex)
        {
            Log?.Invoke($"Center vJoy after stale HID: {ex.Message}");
        }

        try
        {
            _connection.Disconnect();
        }
        catch (Exception ex)
        {
            Log?.Invoke($"[DISCONNECT] Stale HID close error: {ex.Message}");
        }

        _connectedAtUtc = null;
        _lastReadingUtc = null;
        SetConnectionPhase(ConnectionPhase.Reconnecting);
        StartBluetoothRecovery();
    }

    private void HandleUnexpectedDisconnect()
    {
        if (_manualDisconnect)
        {
            return;
        }

        _loggedFirstPoll = false;
        try
        {
            _input.ReleaseAll();
        }
        catch (Exception ex)
        {
            Log?.Invoke($"Release inputs after disconnect: {ex.Message}");
        }

        try
        {
            _vjoy.Center();
        }
        catch (Exception ex)
        {
            Log?.Invoke($"Center vJoy after disconnect: {ex.Message}");
        }

        _connectedAtUtc = null;
        _lastReadingUtc = null;
        SetConnectionPhase(ConnectionPhase.Reconnecting);
        SafeCallbacks.Raise(StatusChanged, "Trying again…");
        SafeCallbacks.Raise(Log, "[DISCONNECT] Balance board disconnected unexpectedly.");
        StartBluetoothRecovery();
    }

    private void StartBluetoothRecovery()
    {
        if (_disposed || _manualDisconnect)
        {
            return;
        }

        if (!Settings.AutoConnectOnStartup)
        {
            SetConnectionPhase(ConnectionPhase.Offline);
            SafeCallbacks.Raise(StatusChanged, "Board disconnected.");
            return;
        }

        if (!DeviceIdRules.ShouldPersistConnectionState(Settings.LastConnectedDeviceId))
        {
            SetConnectionPhase(ConnectionPhase.Offline);
            SafeCallbacks.Raise(StatusChanged, "Board disconnected.");
            return;
        }

        if (_recoveryCts is not null)
        {
            return;
        }

        _recoveryCts = new CancellationTokenSource();
        Log?.Invoke($"[CONNECT] Bluetooth recovery started for {Settings.LastConnectedDeviceId}.");
        _recoveryTask = Task.Run(() => BluetoothRecoveryLoop(_recoveryCts.Token));
    }

    private void StopRecovery()
    {
        _recoveryCts?.Cancel();

        if (_worker.IsCurrentThreadWorker)
        {
            // ConnectWithIntentCore (a manual/quick connect) runs on the ConnectionWorker
            // thread. If the recovery loop is currently blocked inside its own
            // _worker.InvokeStrict call (RunRecoveryConnect) waiting for this very thread,
            // blocking here too would deadlock both sides until timeouts expire. The
            // cancellation above is enough — RunRecoveryConnect checks the token before
            // touching the connection, and the recovery task calls EndRecovery itself
            // (from its own thread) once it unwinds.
            return;
        }

        var task = _recoveryTask;
        if (task is not null)
        {
            try
            {
                task.Wait(TimeSpan.FromSeconds(3));
            }
            catch
            {
                // Recovery teardown must not throw.
            }
        }

        EndRecovery();
    }

    private void EndRecovery()
    {
        var cts = _recoveryCts;
        _recoveryCts = null;
        _recoveryTask = null;
        try
        {
            cts?.Dispose();
        }
        catch
        {
            // Recovery teardown must not throw.
        }
    }

    private void BluetoothRecoveryLoop(CancellationToken ct)
    {
        var delay = BalanceConstants.ReconnectInitialDelayMs;
        var failedAttempts = 0;
        var radioWasOffline = false;
        while (!ct.IsCancellationRequested && !_disposed && !_manualDisconnect)
        {
            try
            {
                if (_connectCts is not null)
                {
                    ct.WaitHandle.WaitOne(delay);
                    continue;
                }

                if (!EnsureBluetoothReady())
                {
                    if (!radioWasOffline)
                    {
                        SafeCallbacks.Raise(Log, "[CONNECT] Bluetooth radio unavailable — pausing recovery.");
                    }

                    radioWasOffline = true;
                    SetConnectionPhase(ConnectionPhase.PairedReconnecting);
                    SafeCallbacks.Raise(StatusChanged, "Waiting for Bluetooth…");
                    if (ct.WaitHandle.WaitOne(delay))
                    {
                        ct.ThrowIfCancellationRequested();
                    }

                    delay = Math.Min(delay * 2, BalanceConstants.ReconnectMaxDelayMs);
                    continue;
                }

                if (radioWasOffline)
                {
                    SafeCallbacks.Raise(Log, "[CONNECT] Bluetooth radio available — running full recovery sequence.");
                    radioWasOffline = false;
                    delay = BalanceConstants.ReconnectInitialDelayMs;
                    if (!WaitForBluetoothRadioReady(ct))
                    {
                        continue;
                    }
                }

                LogBluetoothAdapterState();
                SetConnectionPhase(ConnectionPhase.Reconnecting);
                SafeCallbacks.Raise(StatusChanged, "Trying again…");

                var result = RunRecoveryConnect(failedAttempts, ct);
                if (result.IsSuccess && WaitForFirstBalanceReading(ct))
                {
                    PersistAdapterMacIfKnown();
                    SafeCallbacks.Raise(Log, "[CONNECT] Bluetooth recovery succeeded (first balance reading verified).");
                    EndRecovery();
                    return;
                }

                if (result.IsSuccess)
                {
                    SafeCallbacks.Raise(Log, "[CONNECT] HID open but no balance reading — will retry.");
                    _worker.Invoke(() =>
                    {
                        try
                        {
                            if (_connection.IsConnected)
                            {
                                _connection.Disconnect();
                            }
                        }
                        catch (Exception ex)
                        {
                            SafeCallbacks.Raise(Log, $"[DISCONNECT] Recovery teardown error: {ex.Message}");
                        }

                        _connectedAtUtc = null;
                        _lastReadingUtc = null;
                        _loggedFirstPoll = false;
                        _staleHidHandled = false;
                    });
                }

                failedAttempts++;

                if (ct.WaitHandle.WaitOne(delay))
                {
                    ct.ThrowIfCancellationRequested();
                }

                delay = Math.Min(delay * 2, BalanceConstants.ReconnectMaxDelayMs);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                SafeCallbacks.Raise(Log, $"[CONNECT] Recovery error: {ex.Message}");
                failedAttempts++;
                if (ct.WaitHandle.WaitOne(delay))
                {
                    break;
                }

                delay = Math.Min(delay * 2, BalanceConstants.ReconnectMaxDelayMs);
            }
        }

        EndRecovery();
        if (!_disposed && !_manualDisconnect && !IsSessionHealthy())
        {
            SetConnectionPhase(ConnectionPhase.Offline);
        }
    }

    private ConnectResult RunRecoveryConnect(int failedAttempts, CancellationToken ct)
    {
        // A manual/quick connect (ConnectWithIntentCore) may have queued itself on the
        // ConnectionWorker and already cancelled this recovery token by the time this
        // action actually gets to run. Re-check right before touching the connection so a
        // stale recovery attempt can never race a fresh connect that completed in between
        // (see StopRecovery for the matching deadlock-avoidance half of this fix).
        if (_adapterMacChanged || failedAttempts >= BalanceConstants.RecoveryFullPairAfterAttempts)
        {
            SafeCallbacks.Raise(Log,
                _adapterMacChanged
                    ? "[CONNECT] Recovery: adapter MAC changed — full SYNC pairing (press SYNC if board is flashing)."
                    : "[CONNECT] Recovery: repeated failures — full SYNC pairing (press SYNC if board is flashing).");
            return _worker.InvokeStrict(() =>
            {
                ct.ThrowIfCancellationRequested();
                return TryPairAndConnect(0, 4, ct);
            });
        }

        if (failedAttempts >= BalanceConstants.RecoveryPairAfterAttempts)
        {
            SafeCallbacks.Raise(Log,
                "[CONNECT] Recovery: HID reconnect failed repeatedly — light re-pair (press SYNC if board is flashing).");
            return _worker.InvokeStrict(() =>
            {
                ct.ThrowIfCancellationRequested();
                return TryPairAndConnect(0, 1, ct);
            });
        }

        SafeCallbacks.Raise(Log, $"[CONNECT] Recovery attempt {failedAttempts + 1} — HID reconnect without pairing.");
        return _worker.InvokeStrict(() =>
        {
            ct.ThrowIfCancellationRequested();
            return TryQuickReconnect(0, Settings.LastConnectedDeviceId, ct);
        });
    }

    private bool WaitForBluetoothAtConnectStart(CancellationToken ct)
    {
        if (EnsureBluetoothReady())
        {
            return true;
        }

        var radioWasOffline = false;
        while (!ct.IsCancellationRequested)
        {
            if (EnsureBluetoothReady())
            {
                if (radioWasOffline)
                {
                    Log?.Invoke("[CONNECT] Bluetooth radio available — continuing connect.");
                }

                return true;
            }

            if (!radioWasOffline)
            {
                Log?.Invoke("[CONNECT] Bluetooth radio unavailable — waiting to turn on.");
                StatusChanged?.Invoke("Waiting for Bluetooth… Turn Bluetooth on in Windows settings.");
                DebugSessionTrace.Write(
                    "BalanceBoardSession.cs:WaitForBluetooth",
                    "radio unavailable",
                    "H7",
                    new { adapterMacConfirmed = _adapterMacConfirmedAtConnectStart });
            }

            radioWasOffline = true;
            SetConnectionPhase(ConnectionPhase.PairedReconnecting);

            if (ct.WaitHandle.WaitOne(BalanceConstants.BtRadioReadyPollMs))
            {
                ct.ThrowIfCancellationRequested();
            }
        }

        return false;
    }

    private bool EnsureBluetoothReady()
    {
        if (_pairing is BluetoothPairingService bt)
        {
            return bt.EnsureBluetoothReady(Log);
        }

        return _pairing.IsBluetoothAvailable();
    }

    private bool WaitForBluetoothRadioReady(CancellationToken ct)
    {
        var stablePolls = 0;
        while (!ct.IsCancellationRequested)
        {
            if (EnsureBluetoothReady())
            {
                stablePolls++;
                if (stablePolls >= BalanceConstants.BtRadioReadyStablePolls)
                {
                    SafeCallbacks.Raise(Log, "[CONNECT] Bluetooth radio ready.");
                    return true;
                }
            }
            else
            {
                stablePolls = 0;
            }

            if (ct.WaitHandle.WaitOne(BalanceConstants.BtRadioReadyPollMs))
            {
                ct.ThrowIfCancellationRequested();
            }
        }

        return false;
    }

    private bool WaitForFirstBalanceReading(CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(BalanceConstants.ConnectHealthGraceMs);
        while (!ct.IsCancellationRequested && DateTime.UtcNow < deadline)
        {
            if (_worker.TryInvoke(() => IsSessionHealthy(), out var healthy, false) && healthy)
            {
                return true;
            }

            if (ct.WaitHandle.WaitOne(50))
            {
                ct.ThrowIfCancellationRequested();
            }
        }

        return false;
    }

    private void OnReading(BalanceReading reading)
    {
        if (!reading.IsBalanceBoard)
        {
            return;
        }

        if (!_loggedFirstPoll)
        {
            _loggedFirstPoll = true;
            _lastReadingUtc = DateTime.UtcNow;
            SetConnectionPhase(ConnectionPhase.Connected);
            SafeCallbacks.Raise(StatusChanged, "Connected!");
            Log?.Invoke($"[CONNECT] First balance reading (weight={reading.WeightKg:0.0} kg).");
        }
        else
        {
            _lastReadingUtc = DateTime.UtcNow;
        }

        ProcessedBalance processed;
        try
        {
            processed = _processor.Process(reading, Settings);
        }
        catch (Exception ex)
        {
            Log?.Invoke($"Process error: {ex.Message}");
            Log?.Invoke(ex.StackTrace ?? string.Empty);
            return;
        }

        SafeCallbacks.Raise(Processed, processed);
        LogJumpEdge(processed);

        if (Settings.EnableVJoy && _vjoy.IsReady)
        {
            try
            {
                _vjoy.Update(processed);
            }
            catch (Exception ex)
            {
                Log?.Invoke($"[VJOY] Output error: {ex.Message}");
                Log?.Invoke(ex.StackTrace ?? string.Empty);
            }
        }

        if (!Settings.DisableKeyboardActions)
        {
            try
            {
                _input.Apply(processed, Settings);
            }
            catch (Exception ex)
            {
                Log?.Invoke($"Input simulator error: {ex.Message}");
                Log?.Invoke(ex.StackTrace ?? string.Empty);
            }
        }
    }

    private void LogJumpEdge(ProcessedBalance processed)
    {
        if (processed.Jump && !_wasJumping)
        {
            Log?.Invoke(
                $"[JUMP] Detected weight={processed.WeightKg:0.0}kg " +
                $"threshold={Settings.JumpWeightThresholdKg:0.0}kg " +
                $"profile={Settings.ActiveProfileName} " +
                $"vJoy={Settings.MapJumpToVJoyButton}");
        }

        _wasJumping = processed.Jump;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CancelConnect();
        StopRecovery();
        try
        {
            _worker.Invoke(() =>
            {
                _worker.StopPolling();
                try { _input.ReleaseAll(); } catch { }
                try { _vjoy.Center(); } catch { }
                try { _vjoy.Dispose(); } catch { }
                try { _connection.Dispose(); } catch { }
            });
        }
        catch
        {
            // Dispose must not throw.
        }

        if (_ownsWorker)
        {
            try { _worker.Dispose(); } catch { }
        }
    }
}
