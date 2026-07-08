using System.Text;

namespace BalanceBoard.Core.Services.Diagnostics;

public sealed class DiagnosticsReport
{
    public required string Summary { get; init; }
    public required bool IsHealthy { get; init; }
    public required IReadOnlyList<string> Lines { get; init; }

    public static DiagnosticsReport Run(uint vJoyDeviceId = 1, IReadOnlyList<string>? knownHidDevices = null)
    {
        var lines = new List<string>();
        var healthy = true;

        lines.Add("=== Balance Board Controller — Health Check ===");
        lines.Add($"Time (UTC): {DateTime.UtcNow:O}");
        lines.Add($"OS: {Environment.OSVersion}");
        lines.Add($"64-bit process: {Environment.Is64BitProcess}");
        lines.Add(string.Empty);

        var killed = FeederProcessCleanup.TerminateCompetingFeeders(settleDelayMs: 300);
        if (killed > 0)
        {
            lines.Add($"Stopped {killed} competing feeder process(es):");
            foreach (var name in FeederProcessCleanup.LastTerminatedProcesses)
            {
                lines.Add($"  - {name}");
            }
        }
        else
        {
            lines.Add("No competing feeder processes were running.");
        }

        lines.Add(string.Empty);
        var vjoy = VJoyDiagnostics.Inspect(vJoyDeviceId);
        lines.Add("--- vJoy ---");
        lines.Add($"Driver enabled: {vjoy.DriverEnabled}");
        lines.Add($"Device status: {vjoy.DeviceStatus}");
        lines.Add($"Axes X/Y/Z/RX/RY/RZ: {vjoy.HasAxisX}/{vjoy.HasAxisY}/{vjoy.HasAxisZ}/{vjoy.HasAxisRx}/{vjoy.HasAxisRy}/{vjoy.HasAxisRz}");
        lines.Add($"Buttons configured: {vjoy.ButtonCount}");
        lines.Add($"Driver/DLL match: {vjoy.DriverMatchesDll}");
        if (!string.IsNullOrWhiteSpace(vjoy.Error))
        {
            lines.Add($"Note: {vjoy.Error}");
        }

        if (!vjoy.DriverEnabled || vjoy.DeviceStatus is "VJD_STAT_MISS" or "VJD_STAT_BUSY")
        {
            healthy = false;
        }

        lines.Add(string.Empty);
        lines.Add("--- Wii HID devices ---");
        try
        {
            IReadOnlyList<string> devices;
            if (knownHidDevices is not null)
            {
                devices = knownHidDevices;
                lines.Add("(Skipped HID probe — board session is active.)");
            }
            else
            {
                devices = WiimoteCollectionHelper.DiscoverDeviceIds();
            }

            if (devices.Count == 0)
            {
                lines.Add("No Wii devices detected.");
                lines.Add("Pair the board in Windows Bluetooth settings (PIN 0000, hold SYNC).");
            }
            else
            {
                foreach (var device in devices)
                {
                    lines.Add($"  - {device}");
                }
            }
        }
        catch (Exception ex)
        {
            healthy = false;
            lines.Add($"Discovery error: {ex.Message}");
        }

        lines.Add(string.Empty);
        lines.Add(healthy ? "Result: READY (connect your board to begin)" : "Result: ISSUES FOUND (see notes above)");

        return new DiagnosticsReport
        {
            IsHealthy = healthy,
            Summary = healthy ? "All checks passed" : "Some checks need attention",
            Lines = lines,
        };
    }

    public string ToClipboardText() => string.Join(Environment.NewLine, Lines);
}
