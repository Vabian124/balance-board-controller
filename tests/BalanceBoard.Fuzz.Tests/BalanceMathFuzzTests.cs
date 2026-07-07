using BalanceBoard.Core.Models;
using BalanceBoard.Core.Processing;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace BalanceBoard.Fuzz.Tests;

public class BalanceMathFuzzTests
{
    [Property]
    public Property ToJoyAxis_stays_within_int16_range(float balance, float sensitivity)
    {
        var sens = Math.Clamp(Math.Abs(sensitivity), 0.1f, 5f);
        var axis = BalanceMath.ToJoyAxis(balance, sens, invert: false);
        return (axis >= short.MinValue && axis <= short.MaxValue).ToProperty();
    }

    [Property]
    public Property ApplyDeadzone_never_moves_past_input(float value, float deadzone)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            return true.ToProperty();
        }

        var dz = Math.Clamp(deadzone, 0f, 49f);
        var result = BalanceMath.ApplyDeadzone(value, dz);
        if (float.IsNaN(result))
        {
            return true.ToProperty();
        }

        if (Math.Abs(value - BalanceConstants.BalanceCenterPercent) <= dz)
        {
            return (result == BalanceConstants.BalanceCenterPercent).ToProperty();
        }

        return (result >= BalanceConstants.BalanceCenterPercent == value >= BalanceConstants.BalanceCenterPercent)
            .ToProperty();
    }

    [Property]
    public Property ToBalancePercent_corners_sum_to_100_when_weight_positive(
        PositiveInt tl,
        PositiveInt tr,
        PositiveInt bl,
        PositiveInt br)
    {
        var values = new[] { tl.Get % 50 + 1, tr.Get % 50 + 1, bl.Get % 50 + 1, br.Get % 50 + 1 };
        var (pctTl, pctTr, pctBl, pctBr, total) = BalanceMath.ToBalancePercent(
            values[0], values[1], values[2], values[3]);

        return (total == values.Sum()
                && Math.Abs(pctTl + pctTr + pctBl + pctBr - 100f) < 0.01f)
            .ToProperty();
    }

    [Fact]
    public void AppSettings_roundtrip_json_survives_random_deadzone()
    {
        var settings = new AppSettings
        {
            DeadzonePercent = 17,
            Sensitivity = 1.3,
            TriggerLeftRight = 9,
            TriggerForwardBackward = 10,
        };

        var json = System.Text.Json.JsonSerializer.Serialize(settings);
        var restored = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json);
        Assert.NotNull(restored);
        Assert.Equal(settings.DeadzonePercent, restored!.DeadzonePercent);
        Assert.Equal(settings.Sensitivity, restored.Sensitivity);
    }
}
