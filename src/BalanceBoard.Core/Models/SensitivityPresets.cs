namespace BalanceBoard.Core.Models;

public enum SensitivityLevel
{
    Low,
    Medium,
    High,
    HighlySensitive,
    /// <summary>Tiny lean reaches full stick throw (~2–3% from center).</summary>
    HairTrigger,
}

public enum ThemePreference
{
    System,
    Light,
    Dark,
}

/// <summary>
/// Maps accessibility sensitivity presets to trigger/deadzone/slider values.
/// </summary>
public static class SensitivityPresets
{
    public static void Apply(AppSettings settings, SensitivityLevel level)
    {
        settings.SensitivityLevel = level;
        switch (level)
        {
            case SensitivityLevel.Low:
                settings.Sensitivity = 0.6;
                settings.TriggerLeftRight = 12;
                settings.TriggerForwardBackward = 13;
                settings.TriggerModifierLeftRight = 18;
                settings.TriggerModifierForwardBackward = 19;
                settings.DeadzonePercent = 8;
                break;
            case SensitivityLevel.Medium:
                settings.Sensitivity = 1.0;
                TriggerDefaults.ApplyTo(settings);
                settings.DeadzonePercent = 5;
                break;
            case SensitivityLevel.High:
                settings.Sensitivity = 1.5;
                settings.TriggerLeftRight = 5;
                settings.TriggerForwardBackward = 6;
                settings.TriggerModifierLeftRight = 10;
                settings.TriggerModifierForwardBackward = 11;
                settings.DeadzonePercent = 3;
                break;
            case SensitivityLevel.HighlySensitive:
                settings.Sensitivity = 8.0;
                settings.TriggerLeftRight = 3;
                settings.TriggerForwardBackward = 3;
                settings.TriggerModifierLeftRight = 6;
                settings.TriggerModifierForwardBackward = 7;
                settings.DeadzonePercent = 0;
                break;
            case SensitivityLevel.HairTrigger:
                settings.Sensitivity = 20.0;
                settings.TriggerLeftRight = 2;
                settings.TriggerForwardBackward = 2;
                settings.TriggerModifierLeftRight = 4;
                settings.TriggerModifierForwardBackward = 5;
                settings.DeadzonePercent = 0;
                settings.ResponseCurve = ResponseCurve.Linear;
                break;
        }
    }
}
