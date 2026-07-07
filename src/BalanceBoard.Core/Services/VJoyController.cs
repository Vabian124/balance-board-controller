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

    private vJoy? _joystick;
    private uint _deviceId = 1;
    private bool _acquired;
    private short _lastX;
    private short _lastY;
    private short _lastZ;
    private short _lastRx;
    private short _lastRy;
    private short _lastRz;
    private bool _lastButtonA;
    private bool _axesInitialized;
    private AxisRange _rangeX = SignedFallback;
    private AxisRange _rangeY = SignedFallback;
    private AxisRange _rangeZ = SignedFallback;
    private AxisRange _rangeRx = SignedFallback;
    private AxisRange _rangeRy = SignedFallback;
    private AxisRange _rangeRz = SignedFallback;

    public event Action<string>? Log;

    public bool IsReady => _acquired && _joystick is not null;

    public string? LastError { get; private set; }

    public bool Initialize(uint deviceId = 1, bool attemptCleanupOnBusy = true)
    {
        if (_acquired && _deviceId == deviceId && _joystick is not null)
        {
            return true;
        }

        Shutdown();
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
            LastError = null;
            CacheAxisRanges(deviceId);
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

    public void Update(ProcessedBalance data)
    {
        if (!IsReady || _joystick is null)
        {
            return;
        }

        try
        {
            WriteAxes(data.JoyX, data.JoyY, data.JoyZ, data.JoyRx, data.JoyRy, data.JoyRz, data.VJoyButton1);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Log?.Invoke($"[VJOY] Update failed: {ex.Message}");
        }
    }

    public void Center()
    {
        if (!IsReady || _joystick is null)
        {
            return;
        }

        try
        {
            WriteAxes(0, 0, 0, 0, 0, 0, false);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Log?.Invoke($"[VJOY] Center failed: {ex.Message}");
        }
    }

    private void WriteAxes(short x, short y, short z, short rx, short ry, short rz, bool buttonA)
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

        if (!_axesInitialized || buttonA != _lastButtonA)
        {
            _joystick.SetBtn(buttonA, _deviceId, 1);
            _lastButtonA = buttonA;
        }

        _axesInitialized = true;
    }

    public void Shutdown()
    {
        if (_acquired && _joystick is not null)
        {
            try
            {
                Center();
                _joystick.RelinquishVJD(_deviceId);
            }
            catch
            {
                // ignored
            }
        }

        _acquired = false;
        _axesInitialized = false;
        _joystick = null;
    }

    public void Dispose() => Shutdown();
}
