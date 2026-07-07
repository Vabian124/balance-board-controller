namespace BalanceBoard.Core.Models;

public sealed class ProcessedBalance
{
    public float WeightKg { get; init; }
    public float TopLeftKg { get; init; }
    public float TopRightKg { get; init; }
    public float BottomLeftKg { get; init; }
    public float BottomRightKg { get; init; }
    public float BalanceX { get; init; }
    public float BalanceY { get; init; }
    public bool Jump { get; init; }
    public bool MoveLeft { get; init; }
    public bool MoveRight { get; init; }
    public bool MoveForward { get; init; }
    public bool MoveBackward { get; init; }
    public bool Modifier { get; init; }
    public bool DiagonalLeft { get; init; }
    public bool DiagonalRight { get; init; }
    public short JoyX { get; init; }
    public short JoyY { get; init; }
    public short JoyZ { get; init; }
    public short JoyRx { get; init; }
    public short JoyRy { get; init; }
    public short JoyRz { get; init; }
    public bool ButtonA { get; init; }
}
