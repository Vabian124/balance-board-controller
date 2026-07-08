using BalanceBoard.Core.Models;
using BalanceBoard.Core.Processing;
using Xunit;

namespace BalanceBoard.Core.Tests.Processing;

public class BalanceMathTests
{
    [Fact]
    public void ComputeBalanceXY_zero_distribution_returns_center()
    {
        var (x, y) = BalanceMath.ComputeBalanceXY(0, 0, 0, 0);
        Assert.Equal(BalanceConstants.BalanceCenterPercent, x);
        Assert.Equal(BalanceConstants.BalanceCenterPercent, y);
    }

    [Fact]
    public void ComputeBalanceXY_zero_axes_never_return_origin()
    {
        var (x, y) = BalanceMath.ComputeBalanceXY(0, 0, 0, 0);
        Assert.NotEqual(0f, x);
        Assert.NotEqual(0f, y);
    }

    [Fact]
    public void ToJoyAxis_at_center_is_zero()
    {
        var axis = BalanceMath.ToJoyAxis(BalanceConstants.BalanceCenterPercent, 1.0, invert: false);
        Assert.Equal(0, axis);
    }

    [Fact]
    public void ApplyDeadzone_inside_zone_returns_center()
    {
        var result = BalanceMath.ApplyDeadzone(52f, deadzone: 5);
        Assert.Equal(BalanceConstants.BalanceCenterPercent, result);
    }

    [Fact]
    public void ApplyDeadzone_outside_zone_scales_toward_edge()
    {
        var result = BalanceMath.ApplyDeadzone(70f, deadzone: 5);
        Assert.True(result > BalanceConstants.BalanceCenterPercent);
        Assert.True(result < 70f);
    }

    [Fact]
    public void EvaluateCardinalMovement_detects_left_lean()
    {
        var settings = new AppSettings();
        var (left, right, forward, backward) =
            BalanceMath.EvaluateCardinalMovement(40f, 50f, settings);

        Assert.True(left);
        Assert.False(right);
        Assert.False(forward);
        Assert.False(backward);
    }

    [Fact]
    public void EvaluateCardinalMovement_holds_forward_until_hysteresis_band_crossed()
    {
        var settings = new AppSettings { TriggerForwardBackward = 9 };
        var engageY = BalanceConstants.BalanceCenterPercent - settings.TriggerForwardBackward - 1f;
        var (_, _, forwardOn, _) = BalanceMath.EvaluateCardinalMovement(
            50f, engageY, settings, false, false, false, false);
        Assert.True(forwardOn);

        var insideBandY = BalanceConstants.BalanceCenterPercent - settings.TriggerForwardBackward + 1f;
        var (_, _, stillForward, _) = BalanceMath.EvaluateCardinalMovement(
            50f, insideBandY, settings, false, false, true, false);
        Assert.True(stillForward);

        var releaseY = BalanceConstants.BalanceCenterPercent
            - settings.TriggerForwardBackward
            + BalanceConstants.MovementTriggerHysteresisPercent
            + 1f;
        var (_, _, forwardOff, _) = BalanceMath.EvaluateCardinalMovement(
            50f, releaseY, settings, false, false, true, false);
        Assert.False(forwardOff);
    }

    [Fact]
    public void ToBalancePercent_even_corners_sum_to_100_per_side()
    {
        var (tl, tr, bl, br, total) = BalanceMath.ToBalancePercent(10, 10, 10, 10);
        Assert.Equal(40, total);
        Assert.Equal(25, tl);
        Assert.Equal(50, tl + tr);
        Assert.Equal(50, bl + br);
    }

    [Fact]
    public void EvaluateJump_holds_for_two_seconds_after_lift_off()
    {
        var jumpTime = DateTime.UtcNow.AddSeconds(-1);
        var above = false;
        var stillJumping = BalanceMath.EvaluateJump(0.5f, 1f, 2, DateTime.UtcNow, ref jumpTime, ref above);
        Assert.True(stillJumping);
    }

    [Fact]
    public void EvaluateJump_resets_when_weight_returns()
    {
        var jumpTime = DateTime.UtcNow.AddSeconds(-5);
        var above = false;
        var jumped = BalanceMath.EvaluateJump(60f, 1f, 2, DateTime.UtcNow, ref jumpTime, ref above);
        Assert.False(jumped);
        Assert.True(above);
    }

    [Fact]
    public void EvaluateJump_hold_anchors_on_threshold_crossing()
    {
        var jumpTime = DateTime.UtcNow.AddSeconds(-10);
        var above = true;
        var liftAt = DateTime.UtcNow;

        Assert.True(BalanceMath.EvaluateJump(0.5f, 1f, 2, liftAt, ref jumpTime, ref above));
        Assert.True(liftAt.Subtract(jumpTime).TotalSeconds < 0.01);

        Assert.True(BalanceMath.EvaluateJump(0.5f, 1f, 2, liftAt.AddSeconds(1), ref jumpTime, ref above));
        Assert.False(BalanceMath.EvaluateJump(0.5f, 1f, 2, liftAt.AddSeconds(2.5), ref jumpTime, ref above));
    }

    [Fact]
    public void EvaluateJump_idle_board_does_not_jump()
    {
        var jumpTime = DateTime.MinValue;
        var above = false;
        var jumped = BalanceMath.EvaluateJump(
            0f,
            BalanceConstants.JumpNormalThresholdKg,
            BalanceConstants.JumpNormalHoldSeconds,
            DateTime.UtcNow,
            ref jumpTime,
            ref above);

        Assert.False(jumped);
        Assert.False(above);
    }

    [Fact]
    public void EvaluateDiagonals_detects_right_diagonal_lean()
    {
        var (left, right) = BalanceMath.EvaluateDiagonals(
            total: 50f,
            bottomLeft: 5f,
            topRight: 5f,
            bottomRight: 20f,
            topLeft: 20f,
            moveLeft: false,
            moveRight: false,
            moveForward: false,
            moveBackward: false);

        Assert.False(left);
        Assert.True(right);
    }

    [Fact]
    public void MapCenterOfGravityAxes_applies_deadzone_before_scaling()
    {
        var settings = new AppSettings
        {
            SendCenterOfGravityToAxes = true,
            DeadzonePercent = 5,
            Sensitivity = 1.0,
        };

        var (joyX, joyY) = BalanceMath.MapCenterOfGravityAxes(52f, 50f, settings);
        Assert.Equal(0, joyX);
        Assert.Equal(0, joyY);
    }

    [Fact]
    public void MapCenterOfGravityAxes_high_sensitivity_small_lean_reaches_full_stick()
    {
        var settings = new AppSettings
        {
            SendCenterOfGravityToAxes = true,
            DeadzonePercent = 0,
            Sensitivity = 20.0,
            ResponseCurve = ResponseCurve.Linear,
        };

        var (joyX, _) = BalanceMath.MapCenterOfGravityAxes(52.5f, 50f, settings);

        Assert.InRange(Math.Abs(joyX), 30000, short.MaxValue);
    }

    [Fact]
    public void MapLeanToJoyAxis_sensitivity_one_needs_full_lean_for_max()
    {
        var half = BalanceMath.MapLeanToJoyAxis(75f, 1.0, invert: false, ResponseCurve.Linear);
        var full = BalanceMath.MapLeanToJoyAxis(100f, 1.0, invert: false, ResponseCurve.Linear);

        Assert.InRange(Math.Abs(half), 16000, 17000);
        Assert.Equal(short.MaxValue, full);
    }

    [Fact]
    public void MapCenterOfGravityAxes_exponential_curve_reduces_mid_range_output()
    {
        var linear = new AppSettings
        {
            SendCenterOfGravityToAxes = true,
            DeadzonePercent = 0,
            Sensitivity = 1.0,
            ResponseCurve = ResponseCurve.Linear,
        };
        var exponential = new AppSettings
        {
            SendCenterOfGravityToAxes = true,
            DeadzonePercent = 0,
            Sensitivity = 1.0,
            ResponseCurve = ResponseCurve.Exponential,
        };

        var (linearX, _) = BalanceMath.MapCenterOfGravityAxes(70f, 50f, linear);
        var (expX, _) = BalanceMath.MapCenterOfGravityAxes(70f, 50f, exponential);

        Assert.True(Math.Abs(expX) < Math.Abs(linearX));
    }

    [Fact]
    public void ApplyEllipticalDeadzone_diagonal_lean_reaches_both_axes()
    {
        var (x, y) = BalanceMath.ApplyEllipticalDeadzone(12f, 12f, deadzoneX: 10, deadzoneY: 10);

        Assert.True(x < BalanceConstants.BalanceCenterPercent);
        Assert.True(y < BalanceConstants.BalanceCenterPercent);
        Assert.InRange(x, 0f, 20f);
        Assert.InRange(y, 0f, 20f);
    }

    [Fact]
    public void MapCenterOfGravityAxes_high_sensitivity_with_deadzone_reaches_full_diagonal()
    {
        var settings = new AppSettings
        {
            SendCenterOfGravityToAxes = true,
            DeadzonePercent = 10,
            Sensitivity = 20.0,
            ResponseCurve = ResponseCurve.Linear,
        };

        var (joyX, joyY) = BalanceMath.MapCenterOfGravityAxes(10f, 10f, settings);

        Assert.InRange(Math.Abs(joyX), 25000, short.MaxValue);
        Assert.InRange(Math.Abs(joyY), 25000, short.MaxValue);
        Assert.True(joyX < 0);
        Assert.True(joyY < 0);
    }

    [Fact]
    public void MapCenterOfGravityAxes_lock_left_right_zeros_x()
    {
        var settings = new AppSettings
        {
            SendCenterOfGravityToAxes = true,
            DeadzonePercent = 0,
            Sensitivity = 5.0,
            LockLeftRightAxis = true,
        };

        var (joyX, joyY) = BalanceMath.MapCenterOfGravityAxes(50f, 20f, settings);

        Assert.Equal(0, joyX);
        Assert.NotEqual(0, joyY);
        Assert.True(joyY < 0);
    }

    [Fact]
    public void MapCenterOfGravityAxes_split_sensitivity_uses_per_axis_gain()
    {
        var settings = new AppSettings
        {
            SendCenterOfGravityToAxes = true,
            DeadzonePercent = 0,
            Sensitivity = 1.0,
            SensitivityLeftRight = 20.0,
            SensitivityForwardBackward = 1.0,
            ResponseCurve = ResponseCurve.Linear,
        };

        var (joyX, joyY) = BalanceMath.MapCenterOfGravityAxes(52.5f, 75f, settings);

        Assert.InRange(Math.Abs(joyX), 30000, short.MaxValue);
        Assert.InRange(Math.Abs(joyY), 16000, 17000);
    }

    [Fact]
    public void MapCenterOfGravityAxes_null_split_sensitivity_uses_main_gain()
    {
        var mainOnly = new AppSettings
        {
            SendCenterOfGravityToAxes = true,
            DeadzonePercent = 0,
            Sensitivity = 10.0,
            ResponseCurve = ResponseCurve.Linear,
        };
        var explicitMain = new AppSettings
        {
            SendCenterOfGravityToAxes = true,
            DeadzonePercent = 0,
            Sensitivity = 10.0,
            SensitivityLeftRight = null,
            SensitivityForwardBackward = null,
            ResponseCurve = ResponseCurve.Linear,
        };

        var mainResult = BalanceMath.MapCenterOfGravityAxes(52.5f, 52.5f, mainOnly);
        var explicitResult = BalanceMath.MapCenterOfGravityAxes(52.5f, 52.5f, explicitMain);

        Assert.Equal(mainResult, explicitResult);
    }

    [Fact]
    public void MapCenterOfGravityAxes_per_axis_deadzone_overrides_main()
    {
        var uniform = new AppSettings
        {
            SendCenterOfGravityToAxes = true,
            DeadzonePercent = 5,
            Sensitivity = 5.0,
            ResponseCurve = ResponseCurve.Linear,
        };
        var splitX = new AppSettings
        {
            SendCenterOfGravityToAxes = true,
            DeadzonePercent = 5,
            DeadzoneLeftRightPercent = 0,
            Sensitivity = 5.0,
            ResponseCurve = ResponseCurve.Linear,
        };

        var (uniformX, _) = BalanceMath.MapCenterOfGravityAxes(52f, 50f, uniform);
        var (splitJoyX, _) = BalanceMath.MapCenterOfGravityAxes(52f, 50f, splitX);

        Assert.Equal(0, uniformX);
        Assert.NotEqual(0, splitJoyX);
    }

    [Theory]
    [InlineData(ResponseCurve.Linear, 0f, 0f)]
    [InlineData(ResponseCurve.Linear, 1f, 1f)]
    [InlineData(ResponseCurve.Exponential, 0.5f, 0.25f)]
    [InlineData(ResponseCurve.MinecraftSnappy, 1f, 1f)]
    public void SensitivityCurve_Map_matches_expected(ResponseCurve curve, float input, float expected)
    {
        Assert.Equal(expected, SensitivityCurve.Map(input, curve), precision: 3);
    }

    [Fact]
    public void MapLoadSensorAxes_clamps_instead_of_overflow_wrapping()
    {
        // A glitched/noisy HID reading (or a corner briefly overloaded) can exceed the
        // +-327.67 kg range that the x100 multiplier can represent as a short. Without a
        // clamp this silently wraps to a wildly wrong, possibly opposite-sign axis value.
        var settings = new AppSettings { SendLoadSensorsToAxes = true };
        var reading = new BalanceReading
        {
            TopLeftKg = 1000f,
            TopRightKg = -1000f,
            BottomLeftKg = 0f,
            BottomRightKg = float.NaN,
        };

        var (z, rx, ry, rz) = BalanceMath.MapLoadSensorAxes(reading, settings);

        Assert.Equal(short.MaxValue, z);
        Assert.Equal(short.MinValue, rx);
        Assert.Equal(0, ry);
        Assert.Equal(0, rz);
    }
}
