using BalanceBoard.Core.Models;
using BalanceBoard.Core.Services;
using Xunit;

namespace BalanceBoard.Core.Tests;

public class ConnectionWorkerTests
{
    [Fact]
    public void PollIntervalMs_defaults_to_session_poll_interval()
    {
        using var worker = new ConnectionWorker();
        Assert.Equal(BalanceConstants.SessionPollIntervalMs, worker.PollIntervalMs);
    }

    [Theory]
    [InlineData(5, BalanceConstants.MinPollIntervalMs)]
    [InlineData(0, BalanceConstants.MinPollIntervalMs)]
    [InlineData(-100, BalanceConstants.MinPollIntervalMs)]
    [InlineData(500, BalanceConstants.MaxPollIntervalMs)]
    [InlineData(30, 30)]
    public void PollIntervalMs_is_clamped_to_supported_range(int requested, int expected)
    {
        using var worker = new ConnectionWorker();
        worker.PollIntervalMs = requested;
        Assert.Equal(expected, worker.PollIntervalMs);
    }
}
