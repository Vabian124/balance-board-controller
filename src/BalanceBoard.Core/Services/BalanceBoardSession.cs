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
    private bool _loggedFirstPoll;
    private bool _wasJumping;
    private string? _lastSettingsLogKey;
    private bool _manualDisconnect;
    private DateTime? _connectedAtUtc;
    private DateTime? _lastReadingUtc;
    private ConnectionPhase _connectionPhase = ConnectionPhase.Offline;
    private volatile bool _staleHidHandled;

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

        SyncVJoyFromSettings();
    }

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

        try
        {
            return await Task.Run(() =>
                _worker.InvokeStrict(() => ConnectWithIntentCore(intent, deviceIndex, discoveryRounds, cancellationToken)));
        }
        catch (OperationCanceledException)
        {
            return ConnectResult.Fail(ConnectStatus.Cancelled);
        }
    }

    /// <summary>
    /// QuickReconnect: HID only (boot / second-instance). PairAndConnect: pairing when user asks or --connect.
    /// </summary>
    public ConnectResult ConnectWithIntent(
        ConnectionIntent intent,
        int deviceIndex = 0,
        int discoveryRounds = 4,
        CancellationToken cancellationToken = default) =>
        _worker.InvokeStrict(() => ConnectWithIntentCore(intent, deviceIndex, discoveryRounds, cancellationToken));

    /// <summary>
    /// Legacy entry point — full pairing flow.
    /// </summary>
    public bool ConnectOrPair(int deviceIndex = 0, int discoveryRounds = 4, CancellationToken cancellationToken = default) =>
        ConnectWithIntent(ConnectionIntent.PairAndConnect, deviceIndex, discoveryRounds, cancellationToken).IsSuccess;

    private ConnectResult ConnectWithIntentCore(
        ConnectionIntent intent,
        int deviceIndex,
        int discoveryRounds,
        CancellationToken cancellationToken)
    {
        StopRecovery();
        _manualDisconnect = false;
        ConnectionFlowLogger.LogIntent(Log, intent);
        ConnectionFlowLogger.LogHidDiscovery(Log, _connection.DiscoverDeviceIds());

        _connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            var ct = _connectCts.Token;
            var preferredDeviceId = ResolvePreferredDeviceId();
            if (TryConnect(deviceIndex, preferredDeviceId))
            {
                ConnectionFlowLogger.LogFlowComplete(Log, true);
                return ConnectResult.Ok();
            }

            var result = intent == ConnectionIntent.QuickReconnect
                ? TryQuickReconnect(deviceIndex, preferredDeviceId, ct)
                : TryPairAndConnect(deviceIndex, discoveryRounds, ct);

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
        return true;
    }

    private ConnectResult TryQuickReconnect(int deviceIndex, string? preferredDeviceId, CancellationToken ct)
    {
        Log?.Invoke("Looking for a paired balance board…");
        _pairing.WakePairedDevices(Log);
        ct.ThrowIfCancellationRequested();

        if (!ct.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(BalanceConstants.PostWakeSettleMs)))
        {
            // settle
        }

        ct.ThrowIfCancellationRequested();
        if (TryConnect(deviceIndex, preferredDeviceId))
        {
            return ConnectResult.Ok();
        }

        StatusChanged?.Invoke("Board offline — turn it on or press SYNC, then click Connect.");
        return ConnectResult.Fail(ConnectStatus.NoDevices);
    }

    private ConnectResult TryPairAndConnect(int deviceIndex, int discoveryRounds, CancellationToken ct)
    {
        Log?.Invoke("Press SYNC on the board if it is asleep.");
        var wakeResult = _pairing.PairDiscoverableBoard(Log, ct, removeStalePairings: false);
        if (wakeResult.Success)
        {
            if (!ct.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(BalanceConstants.PostPairSettleMs)))
            {
                // settle after pairing
            }

            ct.ThrowIfCancellationRequested();
            if (TryConnect(deviceIndex, ResolvePreferredDeviceId()))
            {
                return ConnectResult.Ok(wakeResult.Message);
            }

            return ConnectResult.Fail(ConnectStatus.HidFailed, "Paired but HID connect failed.");
        }

        if (!string.IsNullOrWhiteSpace(wakeResult.Message))
        {
            Log?.Invoke(wakeResult.Message);
        }

        for (var round = 1; round <= discoveryRounds; round++)
        {
            ct.ThrowIfCancellationRequested();
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

            if (!ct.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(BalanceConstants.PostPairSettleMs)))
            {
                // settle
            }

            ct.ThrowIfCancellationRequested();
            if (TryConnect(deviceIndex, ResolvePreferredDeviceId()))
            {
                return ConnectResult.Ok(pairResult.Message);
            }
        }

        StatusChanged?.Invoke("Press SYNC on the board, then click Connect.");
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
            SafeCallbacks.Raise(StatusChanged, "Connected — step on the board (use Tare if weight looks wrong).");
            return;
        }

        SetConnectionPhase(ConnectionPhase.Connecting);
        SafeCallbacks.Raise(StatusChanged, "Connecting to balance board…");
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
        SyncVJoyFromSettings();
        Log?.Invoke($"Applied profile: {profileName}");
    }

    private void ApplyPreset(Action<AppSettings> apply, string logMessage)
    {
        apply(Settings);
        SyncVJoyFromSettings();
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
        SafeCallbacks.Raise(StatusChanged, "Board link lost — reconnecting…");

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
        SafeCallbacks.Raise(StatusChanged, "Board disconnected — reconnecting…");
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
        _ = Task.Run(() => BluetoothRecoveryLoop(_recoveryCts.Token));
    }

    private void StopRecovery()
    {
        _recoveryCts?.Cancel();
        EndRecovery();
    }

    private void EndRecovery()
    {
        var cts = _recoveryCts;
        _recoveryCts = null;
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
        while (!ct.IsCancellationRequested && !_disposed && !_manualDisconnect)
        {
            try
            {
                if (_connectCts is not null)
                {
                    ct.WaitHandle.WaitOne(delay);
                    continue;
                }

                if (!_pairing.IsBluetoothAvailable())
                {
                    SetConnectionPhase(ConnectionPhase.PairedReconnecting);
                    SafeCallbacks.Raise(StatusChanged, "Waiting for Bluetooth…");
                    SafeCallbacks.Raise(Log, "[CONNECT] Bluetooth radio offline — waiting.");
                    if (ct.WaitHandle.WaitOne(delay))
                    {
                        ct.ThrowIfCancellationRequested();
                    }

                    delay = Math.Min(delay * 2, BalanceConstants.ReconnectMaxDelayMs);
                    continue;
                }

                SetConnectionPhase(ConnectionPhase.Reconnecting);
                SafeCallbacks.Raise(StatusChanged, "Reconnecting…");
                SafeCallbacks.Raise(Log, "[CONNECT] Recovery attempt — HID reconnect without pairing.");

                var result = _worker.InvokeStrict(() =>
                    TryQuickReconnect(0, Settings.LastConnectedDeviceId, ct));

                if (result.IsSuccess)
                {
                    SafeCallbacks.Raise(Log, "[CONNECT] Bluetooth recovery succeeded.");
                    EndRecovery();
                    return;
                }

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
