namespace BalanceBoard.Core.Models;

public sealed class BalanceReading
{
    public float WeightKg { get; init; }
    public float TopLeftKg { get; init; }
    public float TopRightKg { get; init; }
    public float BottomLeftKg { get; init; }
    public float BottomRightKg { get; init; }
    public bool ButtonA { get; init; }
    public bool IsBalanceBoard { get; init; }
}
