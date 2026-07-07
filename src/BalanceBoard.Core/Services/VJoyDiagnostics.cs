using BalanceBoard.Core.Models;
using vJoyInterfaceWrap;

namespace BalanceBoard.Core.Services;

public sealed class VJoyDiagnostics
{
    public bool DriverEnabled { get; init; }
    public bool DriverMatchesDll { get; init; }
    public string? Error { get; init; }
    public string? Manufacturer { get; init; }
    public string? Product { get; init; }
    public uint DeviceId { get; init; }
    public string DeviceStatus { get; init; } = "Unknown";
    public bool HasAxisX { get; init; }
    public bool HasAxisY { get; init; }
    public bool HasAxisZ { get; init; }
    public bool HasAxisRx { get; init; }
    public bool HasAxisRy { get; init; }
    public bool HasAxisRz { get; init; }
    public int ButtonCount { get; init; }

    public static VJoyDiagnostics Inspect(uint deviceId = 1)
    {
        try
        {
            var joystick = new vJoy();
            if (!joystick.vJoyEnabled())
            {
                return new VJoyDiagnostics
                {
                    DriverEnabled = false,
                    DeviceId = deviceId,
                    Error = "vJoy driver is not enabled. Reboot after installing vJoy, then configure Device 1 in vJoyConf.",
                };
            }

            uint dllVer = 0, drvVer = 0;
            var match = joystick.DriverMatch(ref dllVer, ref drvVer);
            var status = joystick.GetVJDStatus(deviceId);

            return new VJoyDiagnostics
            {
                DriverEnabled = true,
                DriverMatchesDll = match,
                DeviceId = deviceId,
                DeviceStatus = status.ToString(),
                Manufacturer = joystick.GetvJoyManufacturerString(),
                Product = joystick.GetvJoyProductString(),
                HasAxisX = joystick.GetVJDAxisExist(deviceId, HID_USAGES.HID_USAGE_X),
                HasAxisY = joystick.GetVJDAxisExist(deviceId, HID_USAGES.HID_USAGE_Y),
                HasAxisZ = joystick.GetVJDAxisExist(deviceId, HID_USAGES.HID_USAGE_Z),
                HasAxisRx = joystick.GetVJDAxisExist(deviceId, HID_USAGES.HID_USAGE_RX),
                HasAxisRy = joystick.GetVJDAxisExist(deviceId, HID_USAGES.HID_USAGE_RY),
                HasAxisRz = joystick.GetVJDAxisExist(deviceId, HID_USAGES.HID_USAGE_RZ),
                ButtonCount = joystick.GetVJDButtonNumber(deviceId),
                Error = match ? null : $"Driver version 0x{drvVer:X} does not match DLL version 0x{dllVer:X}.",
            };
        }
        catch (Exception ex)
        {
            return new VJoyDiagnostics
            {
                DriverEnabled = false,
                DeviceId = deviceId,
                Error = ex.Message,
            };
        }
    }
}
