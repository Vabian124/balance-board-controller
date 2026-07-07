using BalanceBoard.Core.Models;

namespace BalanceBoard.Core.Processing;

/// <summary>
/// Maps processed balance data to on-screen dot position (percent and canvas coordinates).
/// Shared by WPF visual and unit tests.
/// </summary>
public static class BalanceDisplay
{
    public static (float X, float Y) GetCenterDotPercent(ProcessedBalance data)
    {
        if (data.WeightKg <= BalanceConstants.WeightOnBoardThresholdKg)
        {
            return (BalanceConstants.BalanceCenterPercent, BalanceConstants.BalanceCenterPercent);
        }

        if (data.BalanceX <= BalanceConstants.MinTotalWeightEpsilon
            && data.BalanceY <= BalanceConstants.MinTotalWeightEpsilon)
        {
            return (BalanceConstants.BalanceCenterPercent, BalanceConstants.BalanceCenterPercent);
        }

        return (data.BalanceX, data.BalanceY);
    }

    public static (double Left, double Top) CenterDotCanvasPosition(
        float balanceXPercent,
        float balanceYPercent,
        double canvasWidth,
        double canvasHeight,
        double dotWidth,
        double dotHeight)
    {
        var xRatio = Math.Clamp(balanceXPercent / BalanceConstants.PercentScale, 0, 1);
        var yRatio = Math.Clamp(balanceYPercent / BalanceConstants.PercentScale, 0, 1);
        var dotW = dotWidth > 0 ? dotWidth : 22;
        var dotH = dotHeight > 0 ? dotHeight : 22;
        return (xRatio * (canvasWidth - dotW), yRatio * (canvasHeight - dotH));
    }
}
