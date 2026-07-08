using System.Diagnostics;
using Xunit;

namespace BalanceBoard.Automation;

[Collection("AutomationProcess")]
public class SimulateBoardProcessTests : IDisposable
{
    private Process? _process;

    public void Dispose() => ProcessTestHarness.KillIfRunning(_process);

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

        _process = ProcessTestHarness.StartApp(exe, "--simulate-board --dev --auto-exit-after 3");
        ProcessTestHarness.WaitForExitOrThrow(_process, TimeSpan.FromSeconds(45));
        Assert.True(_process.HasExited, "Simulated board process did not exit cleanly.");

        var latest = Directory.Exists(logDir)
            ? Directory.GetFiles(logDir, "session-*.log")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault()
            : null;

        Assert.NotNull(latest);
        var log = File.ReadAllLines(latest!)
            .SkipWhile(line => !line.Contains("Simulated board: auto-connecting", StringComparison.Ordinal)
                && !line.Contains("[CONNECT]", StringComparison.Ordinal))
            .ToArray();
        if (log.Length == 0)
        {
            log = File.ReadAllLines(latest!);
        }

        Assert.NotEmpty(log);
        Assert.Contains(log, line => line.Contains("[CONNECT]", StringComparison.Ordinal));
        Assert.Contains(log, line => line.Contains("First balance reading", StringComparison.Ordinal));
        Assert.DoesNotContain(log, line => line.Contains("FATAL", StringComparison.Ordinal));
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
