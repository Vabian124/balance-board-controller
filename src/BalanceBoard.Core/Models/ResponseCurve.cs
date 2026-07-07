namespace BalanceBoard.Core.Models;

/// <summary>
/// Maps normalized lean (0–1 from center) to axis output before sensitivity scaling.
/// </summary>
public enum ResponseCurve
{
    Linear,
    EaseInOut,
    Exponential,
    MinecraftSnappy,
}

public static class SensitivityCurve
{
    public static float Map(float normalizedInput, ResponseCurve curve)
    {
        var t = Math.Clamp(normalizedInput, 0f, 1f);
        return curve switch
        {
            ResponseCurve.Linear => t,
            ResponseCurve.EaseInOut => EaseInOut(t),
            ResponseCurve.Exponential => t * t,
            ResponseCurve.MinecraftSnappy => MinecraftSnappy(t),
            _ => t,
        };
    }

    /// <summary>Apply response curve to a balance percent (0–100, center at 50).</summary>
    public static float ApplyToPercent(float percent, ResponseCurve curve)
    {
        var center = BalanceConstants.BalanceCenterPercent;
        var delta = percent - center;
        if (Math.Abs(delta) < 0.001f)
        {
            return center;
        }

        var sign = Math.Sign(delta);
        var normalized = Math.Abs(delta) / center;
        var curved = Map(normalized, curve);
        return center + (float)(sign * curved * center);
    }

    public static string DisplayName(ResponseCurve curve) => curve switch
    {
        ResponseCurve.Linear => "Linear",
        ResponseCurve.EaseInOut => "Ease in/out",
        ResponseCurve.Exponential => "Exponential",
        ResponseCurve.MinecraftSnappy => "Minecraft snappy",
        _ => curve.ToString(),
    };

    private static float EaseInOut(float t) =>
        t < 0.5f
            ? 4f * t * t * t
            : 1f - MathF.Pow(-2f * t + 2f, 3f) / 2f;

    /// <summary>Quick response near center, full range at edge — tuned for block games.</summary>
    private static float MinecraftSnappy(float t) => MathF.Pow(t, 0.65f);
}
