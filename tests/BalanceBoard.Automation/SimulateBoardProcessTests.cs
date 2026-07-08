using System.Diagnostics;
using Xunit;

namespace BalanceBoard.Automation;

public class SimulateBoardProcessTests : IDisposable
{
    public void Dispose() => ProcessTestHarness.StopProcessesByName("BalanceBoardApp");

    [Fact]
    [Trait("Category", "Slow")]
    public void Simulate_board_connects_and_exits_cleanly()
    {
        var root = FindRepoRoot();
        var exe = Path.Combine(
            root,
            "src",
            "BalanceBoard.App",
            "bin",
            "Release",
            "net8.0-windows",
            "BalanceBoardApp.exe");

        Assert.True(File.Exists(exe), $"Build the app first: {exe}");

        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BalanceBoardApp",
            "logs");

        using var proc = ProcessTestHarness.StartApp(exe, "--simulate-board --dev --auto-exit-after 2");
        var started = DateTime.Now;
        ProcessTestHarness.WaitForExitOrThrow(proc);
        Assert.True(proc.HasExited, "Simulated board process did not exit cleanly.");

        var latest = Directory.Exists(logDir)
            ? Directory.GetFiles(logDir, "session-*.log")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault()
            : null;

        Assert.NotNull(latest);
        var log = File.ReadAllLines(latest!)
            .Where(line => TryParseLogTimestamp(line, out var ts) && ts >= started.AddSeconds(-2))
            .ToArray();

        Assert.NotEmpty(log);
        Assert.Contains(log, line => line.Contains("[CONNECT]", StringComparison.Ordinal));
        Assert.Contains(log, line => line.Contains("First balance reading", StringComparison.Ordinal));
        Assert.DoesNotContain(log, line => line.Contains("FATAL", StringComparison.Ordinal));
    }

    private static bool TryParseLogTimestamp(string line, out DateTime utc)
    {
        utc = default;
        if (line.Length < 22 || line[0] != '[')
        {
            return false;
        }

        var end = line.IndexOf(']', 1);
        if (end <= 1)
        {
            return false;
        }

        return DateTime.TryParse(line[1..end], out utc);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "BalanceBoard.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate repo root.");
    }
}
