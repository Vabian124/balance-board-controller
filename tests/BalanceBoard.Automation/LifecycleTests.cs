using System.Diagnostics;
using Xunit;

namespace BalanceBoard.Automation;

[Collection("AutomationProcess")]
public sealed class LifecycleTests : IDisposable
{
    private Process? _process;

    public void Dispose() => ProcessTestHarness.KillIfRunning(_process);

    [Fact]
    public void App_launch_creates_session_log()
    {
        var root = FindRepoRoot();
        var exe = GetAppExe(root);
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BalanceBoardApp",
            "logs");

        _process = ProcessTestHarness.StartApp(exe, "--dev --no-cleanup --allow-multiple --auto-exit-after 2");
        ProcessTestHarness.WaitForExitOrThrow(_process);
        Assert.True(_process.HasExited, "App did not exit cleanly.");

        var latest = Directory.Exists(logDir)
            ? Directory.GetFiles(logDir, "session-*.log")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault()
            : null;

        Assert.NotNull(latest);
        var log = File.ReadAllText(latest!);
        Assert.Contains("Session start", log, StringComparison.Ordinal);
        Assert.Contains("Application shutdown", log, StringComparison.Ordinal);
    }

    private static string GetAppExe(string root)
    {
        var exe = Path.Combine(
            root,
            "src",
            "BalanceBoard.App",
            "bin",
            "Release",
            "net8.0-windows",
            "BalanceBoardApp.exe");
        Assert.True(File.Exists(exe), $"Build the app first: {exe}");
        return exe;
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
