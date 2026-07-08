using BalanceBoard.Core.Models;
using BalanceBoard.Core.Processing;
using BalanceBoard.Core.Services;
using Xunit;

namespace BalanceBoard.Core.Tests;

public class ConnectionStatusTextTests
{
    [Theory]
    [InlineData("Connected to board", UiDetailLevel.Simple, "Connected!")]
    [InlineData("Connected to board", UiDetailLevel.Standard, "Connected to board")]
    [InlineData("Press SYNC to pair", UiDetailLevel.Simple, "Need help pairing — ask a grown-up to press SYNC under the battery cover.")]
    [InlineData("Bluetooth unavailable", UiDetailLevel.Simple, "Waiting for Bluetooth…")]
    [InlineData("Finding board…", UiDetailLevel.Simple, "Finding board…")]
    [InlineData("Try again soon", UiDetailLevel.Simple, "Try again soon")]
    [InlineData("Something else", UiDetailLevel.Simple, "Working on it…")]
    public void FormatStatusForUser_maps_simple_mode(string status, UiDetailLevel level, string expected)
    {
        Assert.Equal(expected, ConnectionStatusText.FormatStatusForUser(status, level));
    }

    [Fact]
    public void FormatConnectFailure_cancelled()
    {
        var result = ConnectResult.Fail(ConnectStatus.Cancelled);
        Assert.Equal(
            "Cancelled.",
            ConnectionStatusText.FormatConnectFailure(ConnectionIntent.PairAndConnect, result, UiDetailLevel.Standard));
    }

    [Fact]
    public void FormatConnectFailure_no_devices_simple()
    {
        var result = ConnectResult.Fail(ConnectStatus.NoDevices);
        Assert.Equal(
            "Board not found — we'll keep trying if auto-connect is on.",
            ConnectionStatusText.FormatConnectFailure(ConnectionIntent.PairAndConnect, result, UiDetailLevel.Simple));
    }

    [Fact]
    public void FormatConnectFailure_no_devices_quick_reconnect_standard()
    {
        var result = ConnectResult.Fail(ConnectStatus.NoDevices);
        Assert.Equal(
            "Board offline — turn it on or press SYNC, then click Connect.",
            ConnectionStatusText.FormatConnectFailure(ConnectionIntent.QuickReconnect, result, UiDetailLevel.Standard));
    }

    [Fact]
    public void FormatConnectFailure_no_devices_pair_standard()
    {
        var result = ConnectResult.Fail(ConnectStatus.NoDevices);
        Assert.Equal(
            "Not found — press SYNC, then Connect again.",
            ConnectionStatusText.FormatConnectFailure(ConnectionIntent.PairAndConnect, result, UiDetailLevel.Standard));
    }

    [Fact]
    public void FormatConnectFailure_pairing_failed_simple()
    {
        var result = ConnectResult.Fail(ConnectStatus.PairingFailed);
        Assert.Equal(
            "Could not find the board — ask a grown-up to press SYNC, then Connect.",
            ConnectionStatusText.FormatConnectFailure(ConnectionIntent.PairAndConnect, result, UiDetailLevel.Simple));
    }

    [Fact]
    public void FormatConnectFailure_pairing_failed_standard()
    {
        var result = ConnectResult.Fail(ConnectStatus.PairingFailed);
        Assert.Equal(
            "Press SYNC on the board, then click Connect.",
            ConnectionStatusText.FormatConnectFailure(ConnectionIntent.PairAndConnect, result, UiDetailLevel.Standard));
    }

    [Fact]
    public void FormatConnectFailure_other_simple()
    {
        var result = ConnectResult.Fail(ConnectStatus.Error, "HID failed");
        Assert.Equal(
            "Something went wrong — see the log for details.",
            ConnectionStatusText.FormatConnectFailure(ConnectionIntent.PairAndConnect, result, UiDetailLevel.Simple));
    }

    [Fact]
    public void FormatConnectFailure_other_standard_uses_message()
    {
        var result = ConnectResult.Fail(ConnectStatus.Error, "HID failed");
        Assert.Equal(
            "HID failed",
            ConnectionStatusText.FormatConnectFailure(ConnectionIntent.PairAndConnect, result, UiDetailLevel.Standard));
    }

    [Fact]
    public void FormatConnectFailure_other_standard_fallback()
    {
        var result = ConnectResult.Fail(ConnectStatus.Error);
        Assert.Equal(
            "Connection failed — see session log.",
            ConnectionStatusText.FormatConnectFailure(ConnectionIntent.PairAndConnect, result, UiDetailLevel.Standard));
    }
}
