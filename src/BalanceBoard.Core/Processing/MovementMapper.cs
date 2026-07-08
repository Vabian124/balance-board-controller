using BalanceBoard.Core.Models;

namespace BalanceBoard.Core.Processing;

/// <summary>
/// Maps <see cref="ProcessedBalance"/> movement flags to <see cref="ActionSlots"/> names.
/// </summary>
public static class MovementMapper
{
    public static bool IsActive(string slot, ProcessedBalance data) =>
        slot switch
        {
            ActionSlots.Left => data.MoveLeft,
            ActionSlots.Right => data.MoveRight,
            ActionSlots.Forward => data.MoveForward,
            ActionSlots.Backward => data.MoveBackward,
            ActionSlots.Modifier => data.Modifier,
            ActionSlots.Jump => data.Jump,
            ActionSlots.DiagonalLeft => data.DiagonalLeft,
            ActionSlots.DiagonalRight => data.DiagonalRight,
            _ => false,
        };

    /// <summary>
    /// Continuous lean magnitude (0..1) for a slot, used to make mouse-move output proportional
    /// to how far the user is leaning instead of a fixed full-speed step.
    /// </summary>
    public static float SlotIntensity(string slot, ProcessedBalance data)
    {
        const float center = BalanceConstants.BalanceCenterPercent;
        var magnitude = slot switch
        {
            ActionSlots.Left => (center - data.BalanceX) / center,
            ActionSlots.Right => (data.BalanceX - center) / center,
            ActionSlots.Forward => (center - data.BalanceY) / center,
            ActionSlots.Backward => (data.BalanceY - center) / center,
            _ => 1f,
        };

        return Math.Clamp(magnitude, 0f, 1f);
    }

    /// <summary>
    /// Scales a mouse-move amount by lean intensity while preserving direction (sign).
    /// </summary>
    public static int ScaleMouseAmount(int amount, float intensity)
    {
        if (amount == 0)
        {
            return 0;
        }

        var scaled = (int)Math.Round(amount * Math.Clamp(intensity, 0f, 1f));
        if (scaled == 0)
        {
            return Math.Sign(amount);
        }

        return scaled;
    }
}
