using System.Diagnostics;
using System.Reflection;

namespace BalanceBoard.Core.Services.Output;

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

    /// <summary>
    /// Substrings that confirm a candidate is our feeder (or the upstream WinForms app),
    /// matched against <see cref="Process.MainModule"/>.FileName when accessible.
    /// </summary>
    private static readonly string[] TrustedPathMarkers =
    [
        "BalanceBoardApp",
        "BalanceBoard.App",
        "WiiBalanceWalker",
        "WBBGUI",
        "wiiboardguirevamp",
        "balance-board-controller",
    ];

    public static IReadOnlyList<string> CompetingProcesses => CompetingProcessNames;

    /// <summary>
    /// Dev / multi-instance mode — do not kill other BalanceBoardApp processes (see <c>--dev</c>).
    /// </summary>
    public static bool AllowMultipleAppInstances =>
        string.Equals(Environment.GetEnvironmentVariable("BALANCEBOARD_DEV"), "1", StringComparison.Ordinal);

    /// <summary>
    /// Terminates other feeder processes. Never kills the current process.
    /// Skips name-colliding processes whose executable path does not look like a known feeder.
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

                        if (!LooksLikeKnownFeeder(process, processName))
                        {
                            Debug.WriteLine(
                                $"Skipping process {processName} pid {process.Id}: path does not match a known feeder.");
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

    /// <summary>
    /// True when the candidate's path looks like a Balance Board / WiiBalanceWalker feeder.
    /// When the path cannot be read (permissions), we still allow kill — process name alone used to be enough,
    /// and denial would leave a real feeder holding vJoy forever.
    /// </summary>
    internal static bool LooksLikeKnownFeeder(Process process, string expectedProcessName)
    {
        string? path = null;
        try
        {
            path = process.MainModule?.FileName;
        }
        catch (Exception ex)
        {
            // AccessDenied / 32-bit vs 64-bit module query — fall through to name-based allow.
            Debug.WriteLine($"Feeder path check unavailable for pid {process.Id}: {ex.Message}");
        }

        return PathLooksLikeKnownFeeder(path, expectedProcessName);
    }

    /// <summary>Pure path predicate for unit tests (no live Process).</summary>
    public static bool PathLooksLikeKnownFeeder(string? filePath, string expectedProcessName)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            // Unreadable path: allow terminate so a real feeder is not left holding vJoy.
            return true;
        }

        var fileName = Path.GetFileNameWithoutExtension(filePath);
        if (fileName.Equals(expectedProcessName, StringComparison.OrdinalIgnoreCase))
        {
            // Exact process-name match on the executable filename is sufficient.
            return true;
        }

        foreach (var marker in TrustedPathMarkers)
        {
            if (filePath.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Path of this assembly used as the "our app" marker for tests and diagnostics.</summary>
    public static string? ThisAppDirectory
    {
        get
        {
            try
            {
                return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            }
            catch
            {
                return null;
            }
        }
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
