using BalanceBoard.Core.Models;

namespace BalanceBoard.Core.Services;

public sealed class BalanceBoardSession : IDisposable
{
    private readonly BalanceBoardConnection _connection = new();
    private readonly BluetoothPairingService _pairing = new();
    private readonly BalanceProcessor _processor = new();
    private readonly VJoyController _vjoy = new();
    private readonly InputSimulator _input = new();
    private readonly System.Timers.Timer _pollTimer = new(50);
    private AppSettings _settings = new();
    private bool _disposed;

    public event Action<ProcessedBalance>? Processed;
    public event Action<string>? Log;
    public event Action<string>? StatusChanged;

    public bool IsConnected => _connection.IsConnected;
    public AppSettings Settings => _settings;

    public BalanceBoardSession()
    {
        _connection.StatusChanged += msg => StatusChanged?.Invoke(msg);
        _connection.Error += msg => Log?.Invoke($"Error: {msg}");
        _vjoy.Log += msg => Log?.Invoke(msg);
        _pollTimer.Elapsed += (_, _) => Poll();
        _pollTimer.AutoReset = true;
    }

    public void LoadSettings(AppSettings settings)
    {
        _settings = settings;
        if (_settings.EnableVJoy)
        {
            _vjoy.Initialize(_settings.VJoyDeviceId);
        }
        else
        {
            _vjoy.Shutdown();
        }
    }

    public IReadOnlyList<string> DiscoverDevices() => _connection.DiscoverDeviceIds();

    public bool Connect(int deviceIndex = 0)
    {
        var ok = _connection.Connect(deviceIndex);
        if (ok)
        {
            OnConnected();
        }
        return ok;
    }

    /// <summary>
    /// Connect to an already-paired board, or automatically Bluetooth-pair then connect (WiiBalanceWalker PIN method).
    /// </summary>
    public bool ConnectOrPair(int deviceIndex = 0, int discoveryRounds = 6)
    {
        if (Connect(deviceIndex))
        {
            return true;
        }

        for (var round = 1; round <= discoveryRounds; round++)
        {
            if (round == 1)
            {
                Log?.Invoke("Balance board not found — starting automatic Bluetooth pairing. Press the red SYNC button.");
            }
            else
            {
                Log?.Invoke($"Still searching… press SYNC again (round {round}/{discoveryRounds}).");
            }

            var pairResult = _pairing.PairDiscoverableBoard(Log);
            Log?.Invoke(pairResult.Message);

            if (!pairResult.Success)
            {
                if (round < discoveryRounds)
                {
                    Thread.Sleep(3000);
                }
                continue;
            }

            Thread.Sleep(1500);
            if (Connect(deviceIndex))
            {
                return true;
            }
        }

        StatusChanged?.Invoke("Press SYNC on the board, then click Connect.");
        return false;
    }

    private void OnConnected()
    {
        _pollTimer.Start();
        if (_settings.AutoTareOnConnect)
        {
            Tare();
        }
        if (_settings.EnableVJoy)
        {
            _vjoy.Initialize(_settings.VJoyDeviceId);
        }
    }

    public void Disconnect()
    {
        _pollTimer.Stop();
        _input.ReleaseAll();
        _vjoy.Center();
        _vjoy.Shutdown();
        _connection.Disconnect();
        StatusChanged?.Invoke("Disconnected.");
    }

    public void Tare()
    {
        _connection.Tare();
        _processor.Tare();
        Log?.Invoke("Balance board tared.");
    }

    public void SetCenter()
    {
        var reading = _connection.GetCurrentReading();
        if (reading is null) return;
        _processor.SetCenterFromCurrentReading(reading);
        Log?.Invoke("Current balance set as center.");
    }

    public void ResetCenter()
    {
        _processor.ResetCenterOffsets();
        Log?.Invoke("Center offset reset.");
    }

    public bool CanSetCenter()
    {
        var reading = _connection.GetCurrentReading();
        return reading is not null && _processor.CanSetCenter(reading.WeightKg);
    }

    public bool CanResetCenter()
    {
        var reading = _connection.GetCurrentReading();
        return reading is not null && _processor.CanResetCenter(reading.WeightKg);
    }

    public void ApplyControllerPreset()
    {
        ActionPresets.ApplyGameController(_settings);
        if (_settings.EnableVJoy)
        {
            _vjoy.Initialize(_settings.VJoyDeviceId);
        }
        else
        {
            _vjoy.Shutdown();
        }

        Log?.Invoke("Applied game controller preset (vJoy X/Y from balance).");
    }

    public void ApplyPedalPreset()
    {
        ActionPresets.ApplyPedal(_settings);
        if (_settings.EnableVJoy)
        {
            _vjoy.Initialize(_settings.VJoyDeviceId);
        }
        else
        {
            _vjoy.Shutdown();
        }

        Log?.Invoke("Applied pedal preset (vJoy Z/RX/RY/RZ from load sensors).");
    }

    public void ApplyKeyboardPreset()
    {
        ActionPresets.ApplyKeyboardMovement(_settings);
        _vjoy.Shutdown();
        Log?.Invoke("Applied hand-free desktop preset (WASD + Shift + Space).");
    }

    public void ApplyProfile(string profileName)
    {
        ActionPresets.Apply(_settings, profileName);
        LoadSettings(_settings);
        Log?.Invoke($"Applied profile: {profileName}");
    }

    private void Poll()
    {
        var reading = _connection.GetCurrentReading();
        if (reading is not null)
        {
            OnReading(reading);
        }
    }

    private void OnReading(BalanceReading reading)
    {
        if (!reading.IsBalanceBoard) return;

        var processed = _processor.Process(reading, _settings);
        Processed?.Invoke(processed);

        if (_settings.EnableVJoy && _vjoy.IsReady)
        {
            _vjoy.Update(processed);
        }

        if (!_settings.DisableKeyboardActions)
        {
            _input.Apply(processed, _settings);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _pollTimer.Stop();
        _pollTimer.Dispose();
        _input.ReleaseAll();
        _vjoy.Center();
        _vjoy.Dispose();
        _connection.Dispose();
    }
}
