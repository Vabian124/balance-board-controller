using BalanceBoard.Core.Services;

var killed = FeederProcessCleanup.TerminateCompetingFeeders();
if (killed > 0)
{
    Console.WriteLine($"Stopped {killed} competing feeder process(es) before validation.");
    foreach (var name in FeederProcessCleanup.LastTerminatedProcesses)
    {
        Console.WriteLine($"  - {name}");
    }
}

FeederProcessCleanup.WaitForVJoyDeviceFree(1);

var diag = VJoyDiagnostics.Inspect(1);
Console.WriteLine("=== vJoy Diagnostics ===");
Console.WriteLine($"Driver enabled: {diag.DriverEnabled}");
Console.WriteLine($"Device status: {diag.DeviceStatus}");
Console.WriteLine($"Axes X/Y/Z/RX/RY/RZ: {diag.HasAxisX}/{diag.HasAxisY}/{diag.HasAxisZ}/{diag.HasAxisRx}/{diag.HasAxisRy}/{diag.HasAxisRz}");
Console.WriteLine($"Buttons: {diag.ButtonCount}");
Console.WriteLine($"DLL match: {diag.DriverMatchesDll}");
if (!string.IsNullOrWhiteSpace(diag.Error)) Console.WriteLine($"Note: {diag.Error}");

using var session = new BalanceBoardSession();
try
{
    var devices = session.DiscoverDevices();
    Console.WriteLine();
    Console.WriteLine("=== Wii HID Devices ===");
    if (devices.Count == 0)
    {
        Console.WriteLine("No Wii devices found (pair balance board in Windows Bluetooth settings).");
    }
    else
    {
        foreach (var device in devices)
        {
            Console.WriteLine($" - {device}");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Device discovery error: {ex.Message}");
}

if (diag.DriverEnabled)
{
    using var vjoy = new VJoyController();
    if (vjoy.Initialize(1))
    {
        Console.WriteLine();
        Console.WriteLine("vJoy acquire: OK — sweeping X axis for 2 seconds...");
        for (var i = 0; i < 40; i++)
        {
            var x = (short)(Math.Sin(i / 6.0) * 16000);
            vjoy.Update(new BalanceBoard.Core.Models.ProcessedBalance
            {
                JoyX = x,
                JoyY = 0,
            });
            Thread.Sleep(50);
        }
        vjoy.Center();
        Console.WriteLine("vJoy axis test complete. Check vJoy Monitor if running.");
    }
    else
    {
        Console.WriteLine($"vJoy acquire failed: {vjoy.LastError}");
    }
}
