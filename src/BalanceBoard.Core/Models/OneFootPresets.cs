namespace BalanceBoard.Core.Models;

/// <summary>
/// Quick preset for one-foot play: high sensitivity, low deadzone, easy jump.
/// </summary>
public static class OneFootPresets
{
    public static void Apply(AppSettings settings)
    {
        settings.OneFootMode = true;
        settings.UseSimpleSensitivity = false;
        SensitivityPresets.Apply(settings, SensitivityLevel.HighlySensitive);
        JumpPresets.Apply(settings, JumpLevel.Easy);
        settings.ResponseCurve = ResponseCurve.EaseInOut;
    }

    public static void Clear(AppSettings settings) => settings.OneFootMode = false;
}
