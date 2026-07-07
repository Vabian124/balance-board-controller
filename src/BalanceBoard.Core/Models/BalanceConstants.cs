namespace BalanceBoard.Core.Models;

/// <summary>
/// Shared thresholds and scaling factors — copy verbatim to Python <c>balance_constants.py</c>.
/// </summary>
public static class BalanceConstants
{
    public const float WeightOnBoardThresholdKg = 5f;
    public const float JumpWeightThresholdKg = 1f;
    public const double JumpHoldSeconds = 2;
    public const float BalanceCenterPercent = 50f;
    public const float DiagonalDeltaThreshold = 15f;
    public const float MinTotalWeightEpsilon = 0.001f;
    public const float PercentScale = 100f;
    public const float JoyAxisScale = 655.34f;
    public const float JoyAxisOffset = -32767f;
    public const double JoySensitivityMultiplier = 2.0;
    public const int SensorKgToAxisMultiplier = 100;
    public const int SessionPollIntervalMs = 50;
}

/// <summary>
/// Default movement trigger percentages (also used by <see cref="ActionPresets"/>).
/// </summary>
public static class TriggerDefaults
{
    public const int LeftRight = 8;
    public const int ForwardBackward = 9;
    public const int ModifierLeftRight = 15;
    public const int ModifierForwardBackward = 16;

    public static void ApplyTo(AppSettings settings)
    {
        settings.TriggerLeftRight = LeftRight;
        settings.TriggerForwardBackward = ForwardBackward;
        settings.TriggerModifierLeftRight = ModifierLeftRight;
        settings.TriggerModifierForwardBackward = ModifierForwardBackward;
    }
}
