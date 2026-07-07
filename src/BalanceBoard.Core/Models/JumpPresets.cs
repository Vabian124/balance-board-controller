namespace BalanceBoard.Core.Models;

public enum JumpLevel
{
    Easy,
    Normal,
    Hard,
}

/// <summary>
/// Maps accessibility jump presets to weight threshold and hold duration.
/// Jump fires when weight drops below threshold; higher threshold = easier jump (one foot off is enough).
/// </summary>
public static class JumpPresets
{
    public static void Apply(AppSettings settings, JumpLevel level)
    {
        settings.JumpLevel = level;
        switch (level)
        {
            case JumpLevel.Easy:
                settings.JumpWeightThresholdKg = BalanceConstants.JumpEasyThresholdKg;
                settings.JumpHoldSeconds = BalanceConstants.JumpEasyHoldSeconds;
                break;
            case JumpLevel.Normal:
                settings.JumpWeightThresholdKg = BalanceConstants.JumpNormalThresholdKg;
                settings.JumpHoldSeconds = BalanceConstants.JumpNormalHoldSeconds;
                break;
            case JumpLevel.Hard:
                settings.JumpWeightThresholdKg = BalanceConstants.JumpHardThresholdKg;
                settings.JumpHoldSeconds = BalanceConstants.JumpHardHoldSeconds;
                break;
        }
    }

    public static string DisplayName(JumpLevel level) => level switch
    {
        JumpLevel.Easy => "Easy to jump",
        JumpLevel.Normal => "Normal jump",
        JumpLevel.Hard => "Hard to jump",
        _ => level.ToString(),
    };
}

public enum UiDetailLevel
{
    Simple,
    Standard,
    Advanced,
}
