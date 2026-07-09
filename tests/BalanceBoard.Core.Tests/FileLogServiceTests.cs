using BalanceBoard.Core.Models;
using BalanceBoard.Core.Services;
using Xunit;

namespace BalanceBoard.Core.Tests;

public class FileLogServiceTests
{
    [Fact]
    public void WriteSessionHeader_includes_stable_version_banner()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bb-tests", Guid.NewGuid().ToString("N"));
        var log = new FileLogService(dir);
        var settings = new AppSettings();

        log.WriteSessionHeader(Path.Combine(dir, "settings.json"), settings);

        var text = File.ReadAllText(log.CurrentLogPath);
        Assert.Contains("[SESSION] Balance Board Controller v", text);
        Assert.Contains("(stable) — reference connect release", text);
        Assert.Contains("=== Session start ===", text);
    }

    [Fact]
    public void WriteException_logs_type_message_and_stack()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bb-tests", Guid.NewGuid().ToString("N"));
        var log = new FileLogService(dir);

        try
        {
            throw new InvalidOperationException("boom");
        }
        catch (Exception ex)
        {
            log.WriteException(ex, "test");
        }

        var text = File.ReadAllText(log.CurrentLogPath);
        Assert.Contains("[ERROR]", text);
        Assert.Contains("InvalidOperationException", text);
        Assert.Contains("boom", text);
    }
}
