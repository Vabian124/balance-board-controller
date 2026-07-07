using BalanceBoard.Core.Services;
using Xunit;

namespace BalanceBoard.Core.Tests;

public class ConnectionFlowLoggerTests
{
    [Fact]
    public void LogIntent_includes_intent_name()
    {
        var lines = Capture(log => ConnectionFlowLogger.LogIntent(log, ConnectionIntent.PairAndConnect));
        Assert.Contains("[CONNECT] Intent=PairAndConnect", lines[0]);
    }

    [Fact]
    public void LogHidDiscovery_lists_device_ids()
    {
        var lines = Capture(log => ConnectionFlowLogger.LogHidDiscovery(log, ["ABC123", "DEF456"]));
        Assert.Contains("2 device(s)", lines[0]);
        Assert.Contains("ABC123", lines[0]);
    }

    [Fact]
    public void LogFlowComplete_reports_success_flag()
    {
        var ok = Capture(log => ConnectionFlowLogger.LogFlowComplete(log, true));
        var fail = Capture(log => ConnectionFlowLogger.LogFlowComplete(log, false));
        Assert.Contains("connected", ok[0]);
        Assert.Contains("not connected", fail[0]);
    }

    private static List<string> Capture(Action<Action<string>?> invoke)
    {
        var lines = new List<string>();
        invoke(lines.Add);
        return lines;
    }
}
