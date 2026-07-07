using System.Diagnostics;

namespace BalanceBoard.Core.Services;

/// <summary>
/// Stops other balance-board / vJoy feeder apps that block device access during development and restarts.
/// </summary>
public static class FeederProcessCleanup
{
    private static readonly string[] CompetingProcessNames =
    [
        "BalanceBoardApp",
        "WiiBalanceWalker",
        "WBBGUI",
    ];

    public static IReadOnlyList<string> CompetingProcesses => CompetingProcessNames;

    /// <summary>
    /// Dev / multi-instance mode — do not kill other BalanceBoardApp processes (see <c>--dev</c>).
    /// </summary>
    public static bool AllowMultipleAppInstances =>
        string.Equals(Environment.GetEnvironmentVariable("BALANCEBOARD_DEV"), "1", StringComparison.Ordinal);

    /// <summary>
    /// Terminates other feeder processes. Never kills the current process.
    /// </summary>
    public static int TerminateCompetingFeeders(int settleDelayMs = 500)
    {
        var currentPid = Environment.ProcessId;
        var killedNames = new List<string>();

        foreach (var processName in CompetingProcessNames)
        {
            if (AllowMultipleAppInstances
                && processName.Equals("BalanceBoardApp", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            Process[] processes;
            try
            {
                processes = Process.GetProcessesByName(processName);
            }
            catch
            {
                continue;
            }

            foreach (var process in processes)
            {
                using (process)
                {
                    if (process.Id == currentPid)
                    {
                        continue;
                    }

                    try
                    {
                        if (process.HasExited)
                        {
                            continue;
                        }

                        process.Kill(entireProcessTree: true);
                        process.WaitForExit(3000);
                        killedNames.Add($"{processName} (pid {process.Id})");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to stop {processName} pid {process.Id}: {ex.Message}");
                    }
                }
            }
        }

        if (killedNames.Count > 0 && settleDelayMs > 0)
        {
            Thread.Sleep(settleDelayMs);
        }

        LastTerminatedProcesses = killedNames;
        return killedNames.Count;
    }

    public static IReadOnlyList<string> LastTerminatedProcesses { get; private set; } = Array.Empty<string>();

    public static bool WaitForVJoyDeviceFree(uint deviceId, int timeoutMs = 3000, int pollMs = 100)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < deadline)
        {
            var diag = VJoyDiagnostics.Inspect(deviceId);
            if (!diag.DriverEnabled)
            {
                return false;
            }

            if (diag.DeviceStatus is "VJD_STAT_FREE" or "VJD_STAT_OWN")
            {
                return true;
            }

            Thread.Sleep(pollMs);
        }

        return false;
    }
}
