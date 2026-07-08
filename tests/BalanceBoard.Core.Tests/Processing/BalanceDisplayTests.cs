using BalanceBoard.Core.Models;
using BalanceBoard.Core.Processing;
using Xunit;

namespace BalanceBoard.Core.Tests;

public class BalanceDisplayTests
{
    [Fact]
    public void GetCenterDotPercent_idle_below_threshold_returns_center_not_origin()
    {
        var idle = new ProcessedBalance
        {
            WeightKg = 0,
            BalanceX = 0,
            BalanceY = 0,
        };

        var (x, y) = BalanceDisplay.GetCenterDotPercent(idle);

        Assert.Equal(BalanceConstants.BalanceCenterPercent, x);
        Assert.Equal(BalanceConstants.BalanceCenterPercent, y);
        Assert.NotEqual(0f, x);
        Assert.NotEqual(0f, y);
    }

    [Fact]
    public void GetCenterDotPercent_phantom_origin_on_board_still_centers()
    {
        var phantom = new ProcessedBalance
        {
            WeightKg = 8,
            BalanceX = 0,
            BalanceY = 0,
        };

        var (x, y) = BalanceDisplay.GetCenterDotPercent(phantom);

        Assert.Equal(BalanceConstants.BalanceCenterPercent, x);
        Assert.Equal(BalanceConstants.BalanceCenterPercent, y);
    }

    [Fact]
    public void CenterDotCanvasPosition_centered_is_not_top_left()
    {
        var (left, top) = BalanceDisplay.CenterDotCanvasPosition(
            BalanceConstants.BalanceCenterPercent,
            BalanceConstants.BalanceCenterPercent,
            canvasWidth: 200,
            canvasHeight: 200,
            dotWidth: 22,
            dotHeight: 22);

        Assert.True(left > 0);
        Assert.True(top > 0);
        Assert.InRange(left, 85, 95);
        Assert.InRange(top, 85, 95);
    }

    [Fact]
    public void CenterDotCanvasPosition_origin_is_top_left()
    {
        var (left, top) = BalanceDisplay.CenterDotCanvasPosition(0, 0, 200, 200, 22, 22);
        Assert.Equal(0, left);
        Assert.Equal(0, top);
    }
}
