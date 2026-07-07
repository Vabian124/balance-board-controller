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
}
