using BalanceBoard.Core.Abstractions;
using BalanceBoard.Core.Models;
using vJoyInterfaceWrap;

namespace BalanceBoard.Core.Services;

public sealed class VJoyController : IGameControllerOutput
{
    private readonly struct AxisRange(long min, long max)
    {
        public long Min { get; } = min;
        public long Max { get; } = max;
        public long Center => (Min + Max) / 2;
    }

    private static readonly AxisRange SignedFallback = new(-32768, 32767);

    private readonly object _sync = new();
    private vJoy? _joystick;
    private uint _deviceId = 1;
    private bool _acquired;
    private short _lastX;
    private short _lastY;
    private short _lastZ;
    private short _lastRx;
    private short _lastRy;
    private short _lastRz;
    private readonly Dictionary<int, bool> _lastButtons = new();
    private bool _axesInitialized;
    private AxisRange _rangeX = SignedFallback;
    private AxisRange _rangeY = SignedFallback;
    private AxisRange _rangeZ = SignedFallback;
    private AxisRange _rangeRx = SignedFallback;
    private AxisRange _rangeRy = SignedFallback;
    private AxisRange _rangeRz = SignedFallback;

    public event Action<string>? Log;

    public bool IsReady
    {
        get
        {
            lock (_sync)
            {
                return _acquired && _joystick is not null;
            }
        }
    }

    public string? LastError { get; private set; }

    public bool Initialize(uint deviceId = 1, bool attemptCleanupOnBusy = true)
    {
        lock (_sync)
        {
            if (_acquired && _deviceId == deviceId && _joystick is not null)
            {
                return true;
            }

            ShutdownUnlocked();
            _deviceId = deviceId;

            if (deviceId is < 1 or > 16)
            {
                LastError = $"Illegal vJoy device id {deviceId}.";
                return false;
            }

            _joystick = new vJoy();
            if (!_joystick.vJoyEnabled())
            {
                LastError = "vJoy driver not enabled.";
                Log?.Invoke($"[VJOY] {LastError}");
                return false;
            }

            Log?.Invoke($"[VJOY] {_joystick.GetvJoyManufacturerString()} / {_joystick.GetvJoyProductString()}");

            return TryAcquire(deviceId, attemptCleanupOnBusy);
        }
    }

    private bool TryAcquire(uint deviceId, bool attemptCleanupOnBusy)
    {
        if (_joystick is null)
        {
            return false;
        }

        var status = _joystick.GetVJDStatus(deviceId);
        if (status == VjdStat.VJD_STAT_MISS)
        {
            LastError = $"vJoy device {deviceId} is not installed. Open vJoyConf and enable it.";
            Log?.Invoke($"[VJOY] {LastError}");
            return false;
        }

        if (status == VjdStat.VJD_STAT_BUSY && attemptCleanupOnBusy)
        {
            Log?.Invoke($"[VJOY] Device {deviceId} busy — stopping other feeder apps and retrying...");
            var killed = FeederProcessCleanup.TerminateCompetingFeeders();
            if (killed > 0)
            {
                Log?.Invoke($"[VJOY] Stopped {killed} competing process(es): {string.Join(", ", FeederProcessCleanup.LastTerminatedProcesses)}");
            }

            FeederProcessCleanup.WaitForVJoyDeviceFree(deviceId);
            status = _joystick.GetVJDStatus(deviceId);
        }

        if (status == VjdStat.VJD_STAT_BUSY)
        {
            LastError = $"vJoy device {deviceId} is owned by another application. Close vJoy Monitor, other feeders, or reboot.";
            Log?.Invoke($"[VJOY] {LastError}");
            return false;
        }

        if (status == VjdStat.VJD_STAT_OWN || _joystick.AcquireVJD(deviceId))
        {
            _joystick.ResetVJD(deviceId);
            _acquired = true;
            _axesInitialized = false;
            _lastButtons.Clear();
            LastError = null;
            CacheAxisRanges(deviceId);
            CenterUnlocked();
            Log?.Invoke($"[VJOY] Acquired device {deviceId}.");
            return true;
        }

        LastError = $"Failed to acquire vJoy device {deviceId}.";
        Log?.Invoke($"[VJOY] {LastError}");
        return false;
    }

    private void CacheAxisRanges(uint deviceId)
    {
        _rangeX = ReadAxisRange(deviceId, HID_USAGES.HID_USAGE_X);
        _rangeY = ReadAxisRange(deviceId, HID_USAGES.HID_USAGE_Y);
        _rangeZ = ReadAxisRange(deviceId, HID_USAGES.HID_USAGE_Z);
        _rangeRx = ReadAxisRange(deviceId, HID_USAGES.HID_USAGE_RX);
        _rangeRy = ReadAxisRange(deviceId, HID_USAGES.HID_USAGE_RY);
        _rangeRz = ReadAxisRange(deviceId, HID_USAGES.HID_USAGE_RZ);
    }

    private AxisRange ReadAxisRange(uint deviceId, HID_USAGES usage)
    {
        if (_joystick is null)
        {
            return SignedFallback;
        }

        try
        {
            long min = 0;
            long max = 0;
            _joystick.GetVJDAxisMin(deviceId, usage, ref min);
            _joystick.GetVJDAxisMax(deviceId, usage, ref max);
            if (max > min)
            {
                return new AxisRange(min, max);
            }
        }
        catch
        {
            // Fall back to signed range.
        }

        return SignedFallback;
    }

    public void Update(ProcessedBalance data, AppSettings settings)
    {
        lock (_sync)
        {
            if (!_acquired || _joystick is null)
            {
                return;
            }

            try
            {
                WriteAxesUnlocked(data.JoyX, data.JoyY, data.JoyZ, data.JoyRx, data.JoyRy, data.JoyRz);
                WriteButtonsUnlocked(data, settings);
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Log?.Invoke($"[VJOY] Update failed: {ex.Message}");
            }
        }
    }

    public void Center()
    {
        lock (_sync)
        {
            CenterUnlocked();
        }
    }

    private void CenterUnlocked()
    {
        if (!_acquired || _joystick is null)
        {
            return;
        }

        try
        {
            WriteAxesUnlocked(0, 0, 0, 0, 0, 0);
            foreach (var button in _lastButtons.Keys.ToList())
            {
                SetButtonUnlocked(button, false);
            }
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Log?.Invoke($"[VJOY] Center failed: {ex.Message}");
        }
    }

    private void WriteAxesUnlocked(short x, short y, short z, short rx, short ry, short rz)
    {
        if (_joystick is null)
        {
            return;
        }

        var deviceX = (int)VJoyAxisMapping.SignedToDevice(x, _rangeX.Min, _rangeX.Max);
        var deviceY = (int)VJoyAxisMapping.SignedToDevice(y, _rangeY.Min, _rangeY.Max);
        var deviceZ = (int)VJoyAxisMapping.SignedToDevice(z, _rangeZ.Min, _rangeZ.Max);
        var deviceRx = (int)VJoyAxisMapping.SignedToDevice(rx, _rangeRx.Min, _rangeRx.Max);
        var deviceRy = (int)VJoyAxisMapping.SignedToDevice(ry, _rangeRy.Min, _rangeRy.Max);
        var deviceRz = (int)VJoyAxisMapping.SignedToDevice(rz, _rangeRz.Min, _rangeRz.Max);

        if (!_axesInitialized || x != _lastX)
        {
            _joystick.SetAxis(deviceX, _deviceId, HID_USAGES.HID_USAGE_X);
            _lastX = x;
        }

        if (!_axesInitialized || y != _lastY)
        {
            _joystick.SetAxis(deviceY, _deviceId, HID_USAGES.HID_USAGE_Y);
            _lastY = y;
        }

        if (!_axesInitialized || z != _lastZ)
        {
            _joystick.SetAxis(deviceZ, _deviceId, HID_USAGES.HID_USAGE_Z);
            _lastZ = z;
        }

        if (!_axesInitialized || rx != _lastRx)
        {
            _joystick.SetAxis(deviceRx, _deviceId, HID_USAGES.HID_USAGE_RX);
            _lastRx = rx;
        }

        if (!_axesInitialized || ry != _lastRy)
        {
            _joystick.SetAxis(deviceRy, _deviceId, HID_USAGES.HID_USAGE_RY);
            _lastRy = ry;
        }

        if (!_axesInitialized || rz != _lastRz)
        {
            _joystick.SetAxis(deviceRz, _deviceId, HID_USAGES.HID_USAGE_RZ);
            _lastRz = rz;
        }

        _axesInitialized = true;
    }

    private void WriteButtonsUnlocked(ProcessedBalance data, AppSettings settings)
    {
        var desired = new Dictionary<int, bool>();

        if (settings.MapJumpToVJoyButton && data.VJoyButton1)
        {
            desired[ClampButton(settings.JumpVJoyButton)] = true;
        }

        if (settings.Actions.TryGetValue(ActionSlots.BoardButton, out var boardBinding)
            && boardBinding.Kind == ActionKind.VJoyButton
            && data.ButtonA)
        {
            desired[ClampButton(boardBinding.VJoyButtonNumber)] = true;
        }

        foreach (var pair in desired)
        {
            SetButtonUnlocked(pair.Key, pair.Value);
        }

        foreach (var tracked in _lastButtons.Keys.ToList())
        {
            if (!desired.ContainsKey(tracked))
            {
                SetButtonUnlocked(tracked, false);
            }
        }
    }

    private void SetButtonUnlocked(int buttonNumber, bool pressed)
    {
        if (_joystick is null)
        {
            return;
        }

        if (_lastButtons.TryGetValue(buttonNumber, out var last) && last == pressed)
        {
            return;
        }

        _joystick.SetBtn(pressed, _deviceId, (uint)buttonNumber);
        _lastButtons[buttonNumber] = pressed;
    }

    private static int ClampButton(int buttonNumber) => Math.Clamp(buttonNumber, 1, 128);

    public void Shutdown()
    {
        lock (_sync)
        {
            ShutdownUnlocked();
        }
    }

    private void ShutdownUnlocked()
    {
        if (_acquired && _joystick is not null)
        {
            try
            {
                CenterUnlocked();
                _joystick.RelinquishVJD(_deviceId);
            }
            catch
            {
                // ignored
            }
        }

        _acquired = false;
        _axesInitialized = false;
        _lastButtons.Clear();
        _joystick = null;
    }

    public void Dispose() => Shutdown();
}
