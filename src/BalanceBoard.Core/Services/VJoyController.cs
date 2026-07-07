using BalanceBoard.Core.Models;
using vJoyInterfaceWrap;

namespace BalanceBoard.Core.Services;

public sealed class VJoyController : IDisposable
{
    private vJoy? _joystick;
    private uint _deviceId = 1;
    private bool _acquired;

    public event Action<string>? Log;

    public bool IsReady => _acquired && _joystick is not null;

    public string? LastError { get; private set; }

    public bool Initialize(uint deviceId = 1, bool attemptCleanupOnBusy = true)
    {
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
            Log?.Invoke(LastError);
            return false;
        }

        Log?.Invoke($"vJoy: {_joystick.GetvJoyManufacturerString()} / {_joystick.GetvJoyProductString()}");

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
            Log?.Invoke(LastError);
            return false;
        }

        if (status == VjdStat.VJD_STAT_BUSY && attemptCleanupOnBusy)
        {
            Log?.Invoke($"vJoy device {deviceId} is busy — stopping other feeder apps and retrying...");
            var killed = FeederProcessCleanup.TerminateCompetingFeeders();
            if (killed > 0)
            {
                Log?.Invoke($"Stopped {killed} competing process(es): {string.Join(", ", FeederProcessCleanup.LastTerminatedProcesses)}");
            }

            FeederProcessCleanup.WaitForVJoyDeviceFree(deviceId);
            status = _joystick.GetVJDStatus(deviceId);
        }

        if (status == VjdStat.VJD_STAT_BUSY)
        {
            LastError = $"vJoy device {deviceId} is owned by another application. Close vJoy Monitor, other feeders, or reboot.";
            Log?.Invoke(LastError);
            return false;
        }

        if (status == VjdStat.VJD_STAT_OWN || _joystick.AcquireVJD(deviceId))
        {
            _joystick.ResetVJD(deviceId);
            _acquired = true;
            LastError = null;
            Log?.Invoke($"Acquired vJoy device {deviceId}.");
            return true;
        }

        LastError = $"Failed to acquire vJoy device {deviceId}.";
        Log?.Invoke(LastError);
        return false;
    }

    public void Update(ProcessedBalance data)
    {
        if (!IsReady || _joystick is null) return;

        _joystick.SetAxis(data.JoyX, _deviceId, HID_USAGES.HID_USAGE_X);
        _joystick.SetAxis(data.JoyY, _deviceId, HID_USAGES.HID_USAGE_Y);
        _joystick.SetAxis(data.JoyZ, _deviceId, HID_USAGES.HID_USAGE_Z);
        _joystick.SetAxis(data.JoyRx, _deviceId, HID_USAGES.HID_USAGE_RX);
        _joystick.SetAxis(data.JoyRy, _deviceId, HID_USAGES.HID_USAGE_RY);
        _joystick.SetAxis(data.JoyRz, _deviceId, HID_USAGES.HID_USAGE_RZ);
        _joystick.SetBtn(data.ButtonA, _deviceId, 1);
    }

    public void Center()
    {
        if (!IsReady || _joystick is null) return;

        _joystick.SetAxis(0, _deviceId, HID_USAGES.HID_USAGE_X);
        _joystick.SetAxis(0, _deviceId, HID_USAGES.HID_USAGE_Y);
        _joystick.SetAxis(0, _deviceId, HID_USAGES.HID_USAGE_Z);
        _joystick.SetAxis(0, _deviceId, HID_USAGES.HID_USAGE_RX);
        _joystick.SetAxis(0, _deviceId, HID_USAGES.HID_USAGE_RY);
        _joystick.SetAxis(0, _deviceId, HID_USAGES.HID_USAGE_RZ);
        _joystick.SetBtn(false, _deviceId, 1);
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
        _joystick = null;
    }

    public void Dispose() => Shutdown();
}
