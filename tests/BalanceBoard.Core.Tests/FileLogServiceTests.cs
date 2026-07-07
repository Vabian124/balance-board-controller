using BalanceBoard.Core.Services;
using Xunit;

namespace BalanceBoard.Core.Tests;

public class FileLogServiceTests
{
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
