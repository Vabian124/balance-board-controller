namespace BalanceBoard.Core.Models;

/// <summary>
/// Canonical action slot names — single source for presets, input simulator, and settings JSON keys.
/// </summary>
public static class ActionSlots
{
    public const string Left = "Left";
    public const string Right = "Right";
    public const string Forward = "Forward";
    public const string Backward = "Backward";
    public const string Modifier = "Modifier";
    public const string Jump = "Jump";
    public const string DiagonalLeft = "DiagonalLeft";
    public const string DiagonalRight = "DiagonalRight";
    public const string BoardButton = "BoardButton";

    public static IReadOnlyList<string> All { get; } =
    [
        Left,
        Right,
        Forward,
        Backward,
        Modifier,
        Jump,
        DiagonalLeft,
        DiagonalRight,
        BoardButton,
    ];

    /// <summary>Lean / jump movement slots — excludes the physical board button.</summary>
    public static IReadOnlyList<string> Movement { get; } =
    [
        Left,
        Right,
        Forward,
        Backward,
        Modifier,
        Jump,
        DiagonalLeft,
        DiagonalRight,
    ];
}
