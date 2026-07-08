using System.Diagnostics;
using System.Text;

namespace BalanceBoard.Automation;

internal static class ProcessTestHarness
{
    private static readonly TimeSpan DefaultExitTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CleanupTimeout = TimeSpan.FromSeconds(5);

    public static Process StartApp(string exe, string arguments)
    {
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = exe,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        }) ?? throw new InvalidOperationException("Failed to start BalanceBoardApp.");

        _ = process.StandardOutput.ReadToEndAsync();
        _ = process.StandardError.ReadToEndAsync();
        return process;
    }

    public static void WaitForExitOrThrow(Process process, TimeSpan? timeout = null)
    {
        var effectiveTimeout = timeout ?? DefaultExitTimeout;
        var pid = process.Id;
        var name = process.ProcessName;
        if (process.WaitForExit((int)effectiveTimeout.TotalMilliseconds))
        {
            return;
        }

        try
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit((int)CleanupTimeout.TotalMilliseconds);
        }
        catch
        {
            // Preserve the original timeout failure below.
        }

        throw new TimeoutException(
            $"Process {name} (pid {pid}) did not exit within {effectiveTimeout.TotalSeconds:0.#} seconds.");
    }

    public static void KillIfRunning(Process? process)
    {
        if (process is null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit((int)CleanupTimeout.TotalMilliseconds);
            }
        }
        catch
        {
            // Best-effort cleanup for automation tests.
        }
    }

    public static void StopProcessesByName(string name)
    {
        foreach (var process in Process.GetProcessesByName(name))
        {
            using (process)
            {
                KillIfRunning(process);
            }
        }
    }

    public static bool WaitForProcessCountAtLeast(string name, int minimumCount, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (Process.GetProcessesByName(name).Length >= minimumCount)
            {
                return true;
            }

            Thread.Sleep(200);
        }

        return false;
    }
}
