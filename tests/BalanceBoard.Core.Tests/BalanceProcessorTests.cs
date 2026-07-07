using BalanceBoard.Core.Abstractions;
using BalanceBoard.Core.Models;
using BalanceBoard.Core.Processing;
using BalanceBoard.Core.Services;
using Xunit;

namespace BalanceBoard.Core.Tests;

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

    [Theory]
    [InlineData(ResponseCurve.Linear, 0f, 0f)]
    [InlineData(ResponseCurve.Linear, 1f, 1f)]
    [InlineData(ResponseCurve.Exponential, 0.5f, 0.25f)]
    [InlineData(ResponseCurve.MinecraftSnappy, 1f, 1f)]
    public void SensitivityCurve_Map_matches_expected(ResponseCurve curve, float input, float expected)
    {
        Assert.Equal(expected, SensitivityCurve.Map(input, curve), precision: 3);
    }
}

public class OneFootPresetsTests
{
    [Fact]
    public void Apply_sets_high_sensitivity_easy_jump_and_curve()
    {
        var settings = new AppSettings();
        OneFootPresets.Apply(settings);

        Assert.True(settings.OneFootMode);
        Assert.False(settings.UseSimpleSensitivity);
        Assert.Equal(SensitivityLevel.HairTrigger, settings.SensitivityLevel);
        Assert.Equal(JumpLevel.Easy, settings.JumpLevel);
        Assert.Equal(ResponseCurve.EaseInOut, settings.ResponseCurve);
        Assert.Equal(0, settings.DeadzonePercent);
    }
}

public class VirtualKeyCodesTests
{
    [Theory]
    [InlineData("A", 0x41)]
    [InlineData("w", 0x57)]
    [InlineData("Space", 0x20)]
    [InlineData("LShiftKey", 0xA0)]
    public void TryGet_resolves_preset_keys(string name, ushort expected)
    {
        Assert.True(VirtualKeyCodes.TryGet(name, out var vk));
        Assert.Equal(expected, vk);
    }
}

public class BalanceProcessorTests
{
    [Fact]
    public void Process_even_weight_produces_centered_balance()
    {
        var processor = new BalanceProcessor();
        var settings = new AppSettings();
        var reading = new BalanceReading
        {
            WeightKg = 60,
            TopLeftKg = 15,
            TopRightKg = 15,
            BottomLeftKg = 15,
            BottomRightKg = 15,
            IsBalanceBoard = true,
        };

        processor.Tare();
        var processed = processor.Process(reading, settings);

        Assert.InRange(processed.BalanceX, 49f, 51f);
        Assert.InRange(processed.BalanceY, 49f, 51f);
        Assert.False(processed.MoveLeft);
        Assert.False(processed.MoveRight);
        Assert.Equal(0, processed.JoyX);
        Assert.Equal(0, processed.JoyY);
    }

    [Fact]
    public void Process_left_lean_sets_move_left()
    {
        var processor = new BalanceProcessor();
        var settings = new AppSettings();
        var reading = new BalanceReading
        {
            WeightKg = 60,
            TopLeftKg = 25,
            TopRightKg = 5,
            BottomLeftKg = 25,
            BottomRightKg = 5,
            IsBalanceBoard = true,
        };

        processor.Tare();
        var processed = processor.Process(reading, settings);

        Assert.True(processed.MoveLeft);
        Assert.False(processed.MoveRight);
        Assert.True(processed.BalanceX < BalanceConstants.BalanceCenterPercent);
    }

    [Fact]
    public void Process_diagonal_lean_without_cardinal_movement()
    {
        var processor = new BalanceProcessor();
        var settings = new AppSettings();
        var reading = new BalanceReading
        {
            WeightKg = 50,
            TopLeftKg = 20,
            TopRightKg = 5,
            BottomLeftKg = 5,
            BottomRightKg = 20,
            IsBalanceBoard = true,
        };

        processor.Tare();
        var processed = processor.Process(reading, settings);

        Assert.False(processed.MoveLeft);
        Assert.False(processed.MoveRight);
        Assert.False(processed.MoveForward);
        Assert.False(processed.MoveBackward);
        Assert.True(processed.DiagonalRight);
        Assert.False(processed.DiagonalLeft);
    }

    [Fact]
    public void Process_light_weight_with_no_corner_load_is_centered()
    {
        var processor = new BalanceProcessor();
        var settings = new AppSettings();
        var reading = new BalanceReading
        {
            WeightKg = 8,
            TopLeftKg = 0,
            TopRightKg = 0,
            BottomLeftKg = 0,
            BottomRightKg = 0,
            IsBalanceBoard = true,
        };

        processor.Tare();
        var processed = processor.Process(reading, settings);

        Assert.Equal(BalanceConstants.BalanceCenterPercent, processed.BalanceX);
        Assert.Equal(BalanceConstants.BalanceCenterPercent, processed.BalanceY);
    }

    [Fact]
    public void Process_minecraft_preset_one_foot_jump_triggers()
    {
        var processor = new BalanceProcessor();
        var settings = new AppSettings();
        ActionPresets.ApplyMinecraft(settings);

        var standing = new BalanceReading
        {
            WeightKg = 70,
            TopLeftKg = 17,
            TopRightKg = 17,
            BottomLeftKg = 18,
            BottomRightKg = 18,
            IsBalanceBoard = true,
        };
        var oneFoot = new BalanceReading
        {
            WeightKg = 32,
            TopLeftKg = 8,
            TopRightKg = 8,
            BottomLeftKg = 8,
            BottomRightKg = 8,
            IsBalanceBoard = true,
        };

        processor.Tare();
        processor.Process(standing, settings);
        var jumped = processor.Process(oneFoot, settings);

        Assert.True(jumped.Jump);
        Assert.True(jumped.VJoyButton1);
        Assert.Equal(BalanceConstants.JumpNormalThresholdKg, settings.JumpWeightThresholdKg);
    }

    [Fact]
    public void Process_one_foot_jump_triggers_below_threshold()
    {
        var processor = new BalanceProcessor();
        var settings = new AppSettings
        {
            JumpWeightThresholdKg = 40f,
            JumpHoldSeconds = 2,
            MapJumpToVJoyButton = true,
        };
        var standing = new BalanceReading
        {
            WeightKg = 70,
            TopLeftKg = 17,
            TopRightKg = 17,
            BottomLeftKg = 18,
            BottomRightKg = 18,
            IsBalanceBoard = true,
        };
        var oneFoot = new BalanceReading
        {
            WeightKg = 35,
            TopLeftKg = 8,
            TopRightKg = 8,
            BottomLeftKg = 9,
            BottomRightKg = 10,
            IsBalanceBoard = true,
        };

        processor.Tare();
        processor.Process(standing, settings);
        var jumped = processor.Process(oneFoot, settings);

        Assert.True(jumped.Jump);
        Assert.True(jumped.VJoyButton1);
    }

    [Fact]
    public void Process_idle_board_is_centered_with_neutral_vjoy()
    {
        var processor = new BalanceProcessor();
        var settings = new AppSettings { SendCenterOfGravityToAxes = true };
        var reading = new BalanceReading
        {
            WeightKg = 0,
            TopLeftKg = 0,
            TopRightKg = 0,
            BottomLeftKg = 0,
            BottomRightKg = 0,
            IsBalanceBoard = true,
        };

        processor.Tare();
        var processed = processor.Process(reading, settings);

        Assert.Equal(BalanceConstants.BalanceCenterPercent, processed.BalanceX);
        Assert.Equal(BalanceConstants.BalanceCenterPercent, processed.BalanceY);
        Assert.Equal(0, processed.JoyX);
        Assert.Equal(0, processed.JoyY);
        Assert.False(processed.MoveLeft);
        Assert.False(processed.Jump);
    }

    [Fact]
    public void Process_negative_weight_idle_is_centered_not_origin()
    {
        var processor = new BalanceProcessor();
        var settings = new AppSettings();
        var reading = new BalanceReading
        {
            WeightKg = -7.6f,
            TopLeftKg = -2f,
            TopRightKg = -1.9f,
            BottomLeftKg = -2.1f,
            BottomRightKg = -1.8f,
            IsBalanceBoard = true,
        };

        processor.Tare();
        var processed = processor.Process(reading, settings);

        Assert.Equal(BalanceConstants.BalanceCenterPercent, processed.BalanceX);
        Assert.Equal(BalanceConstants.BalanceCenterPercent, processed.BalanceY);
        Assert.NotEqual(0f, processed.BalanceX);
        Assert.NotEqual(0f, processed.BalanceY);
    }

    [Fact]
    public void Process_jump_after_brief_lift_off()
    {
        var processor = new BalanceProcessor();
        var settings = new AppSettings { JumpWeightThresholdKg = 1f, JumpHoldSeconds = 2 };
        var onBoard = new BalanceReading
        {
            WeightKg = 60,
            TopLeftKg = 15,
            TopRightKg = 15,
            BottomLeftKg = 15,
            BottomRightKg = 15,
            IsBalanceBoard = true,
        };
        var air = new BalanceReading
        {
            WeightKg = 0.2f,
            TopLeftKg = 0.2f,
            TopRightKg = 0.2f,
            BottomLeftKg = 0.2f,
            BottomRightKg = 0.2f,
            IsBalanceBoard = true,
        };

        processor.Tare();
        processor.Process(onBoard, settings);
        var jumped = processor.Process(air, settings);

        Assert.True(jumped.Jump);
    }

    [Fact]
    public void Process_map_jump_to_vjoy_button_when_enabled()
    {
        var processor = new BalanceProcessor();
        var settings = new AppSettings
        {
            JumpWeightThresholdKg = 1f,
            JumpHoldSeconds = 2,
            MapJumpToVJoyButton = true,
        };
        var onBoard = new BalanceReading
        {
            WeightKg = 60,
            TopLeftKg = 15,
            TopRightKg = 15,
            BottomLeftKg = 15,
            BottomRightKg = 15,
            IsBalanceBoard = true,
        };
        var air = new BalanceReading
        {
            WeightKg = 0.2f,
            TopLeftKg = 0.2f,
            TopRightKg = 0.2f,
            BottomLeftKg = 0.2f,
            BottomRightKg = 0.2f,
            IsBalanceBoard = true,
        };

        processor.Tare();
        processor.Process(onBoard, settings);
        var jumped = processor.Process(air, settings);

        Assert.True(jumped.Jump);
        Assert.True(jumped.VJoyButton1);
        Assert.False(jumped.ButtonA);
    }
}

public class ActionPresetsTests
{
    [Fact]
    public void ApplyGameController_enables_vjoy_xy()
    {
        var settings = new AppSettings();
        ActionPresets.ApplyGameController(settings);
        Assert.Equal(ActionPresets.GameController, settings.ActiveProfileName);
        Assert.True(settings.EnableVJoy);
        Assert.True(settings.SendCenterOfGravityToAxes);
        Assert.False(settings.SendLoadSensorsToAxes);
        Assert.True(settings.DisableKeyboardActions);
        Assert.False(settings.MapJumpToVJoyButton);
    }

    [Fact]
    public void ApplyMinecraft_maps_jump_to_vjoy_and_move_axes()
    {
        var settings = new AppSettings();
        ActionPresets.ApplyMinecraft(settings);
        Assert.Equal(ActionPresets.Minecraft, settings.ActiveProfileName);
        Assert.True(settings.EnableVJoy);
        Assert.True(settings.SendCenterOfGravityToAxes);
        Assert.True(settings.MapJumpToVJoyButton);
        Assert.Equal(BalanceConstants.JumpNormalThresholdKg, settings.JumpWeightThresholdKg);
        Assert.Equal(BalanceConstants.JumpNormalHoldSeconds, settings.JumpHoldSeconds);
        Assert.Equal(JumpLevel.Normal, settings.JumpLevel);
        Assert.True(settings.DisableKeyboardActions);
        Assert.Equal(SensitivityLevel.Medium, settings.SensitivityLevel);
        Assert.Equal(ResponseCurve.MinecraftSnappy, settings.ResponseCurve);
    }

    [Fact]
    public void ApplyMinecraft_uses_normal_jump_preset()
    {
        var settings = new AppSettings();
        ActionPresets.ApplyMinecraft(settings);
        Assert.Equal(BalanceConstants.JumpNormalHoldSeconds, settings.JumpHoldSeconds);
    }

    [Fact]
    public void ApplyPedal_enables_sensor_axes()
    {
        var settings = new AppSettings();
        ActionPresets.ApplyPedal(settings);
        Assert.False(settings.SendCenterOfGravityToAxes);
        Assert.True(settings.SendLoadSensorsToAxes);
    }

    [Fact]
    public void ApplyKeyboardMovement_binds_jump_to_mouse_click()
    {
        var settings = new AppSettings();
        ActionPresets.ApplyKeyboardMovement(settings);
        var jump = settings.Actions[ActionSlots.Jump];
        Assert.Equal(ActionKind.MouseButton, jump.Kind);
        Assert.Equal("Left", jump.MouseButton);
    }

    [Fact]
    public void ApplyBalanceMouse_maps_lean_to_cursor()
    {
        var settings = new AppSettings();
        ActionPresets.ApplyBalanceMouse(settings);
        Assert.Equal(ActionKind.MouseMoveX, settings.Actions[ActionSlots.Left].Kind);
        Assert.Equal(ActionKind.MouseMoveY, settings.Actions[ActionSlots.Forward].Kind);
    }
}

public class JumpPresetsTests
{
    [Theory]
    [InlineData(JumpLevel.Easy, 20f, 0.6)]
    [InlineData(JumpLevel.Normal, 35f, 0.4)]
    [InlineData(JumpLevel.Hard, 50f, 0.25)]
    public void Apply_sets_threshold_and_hold(JumpLevel level, float threshold, double holdSeconds)
    {
        var settings = new AppSettings();
        JumpPresets.Apply(settings, level);
        Assert.Equal(level, settings.JumpLevel);
        Assert.Equal(threshold, settings.JumpWeightThresholdKg);
        Assert.Equal(holdSeconds, settings.JumpHoldSeconds);
    }

    [Fact]
    public void Hard_uses_higher_threshold_than_easy()
    {
        var easy = new AppSettings();
        var hard = new AppSettings();
        JumpPresets.Apply(easy, JumpLevel.Easy);
        JumpPresets.Apply(hard, JumpLevel.Hard);
        Assert.True(hard.JumpWeightThresholdKg > easy.JumpWeightThresholdKg);
    }

    [Fact]
    public void Normal_matches_minecraft_preset_jump_defaults()
    {
        var fromPreset = new AppSettings();
        ActionPresets.ApplyMinecraft(fromPreset);
        var fromJump = new AppSettings();
        JumpPresets.Apply(fromJump, JumpLevel.Normal);
        Assert.Equal(fromJump.JumpWeightThresholdKg, fromPreset.JumpWeightThresholdKg);
        Assert.Equal(fromJump.JumpHoldSeconds, fromPreset.JumpHoldSeconds);
        Assert.Equal(JumpLevel.Normal, fromPreset.JumpLevel);
    }

    [Fact]
    public void Minecraft_jump_triggers_after_one_foot_lift()
    {
        var processor = new BalanceProcessor();
        var settings = new AppSettings();
        ActionPresets.ApplyMinecraft(settings);
        var onBoard = new BalanceReading
        {
            WeightKg = 60,
            TopLeftKg = 15,
            TopRightKg = 15,
            BottomLeftKg = 15,
            BottomRightKg = 15,
            IsBalanceBoard = true,
        };
        var oneFoot = new BalanceReading
        {
            WeightKg = 28,
            TopLeftKg = 28,
            TopRightKg = 0,
            BottomLeftKg = 0,
            BottomRightKg = 0,
            IsBalanceBoard = true,
        };

        processor.Tare();
        processor.Process(onBoard, settings);
        var jumped = processor.Process(oneFoot, settings);

        Assert.True(jumped.Jump);
        Assert.True(jumped.VJoyButton1);
    }
}

public class SensitivityPresetsTests
{
    [Fact]
    public void HighlySensitive_lowers_triggers()
    {
        var settings = new AppSettings();
        SensitivityPresets.Apply(settings, SensitivityLevel.HighlySensitive);
        Assert.Equal(3, settings.TriggerLeftRight);
        Assert.Equal(8.0, settings.Sensitivity);
        Assert.Equal(0, settings.DeadzonePercent);
    }

    [Fact]
    public void HairTrigger_uses_maximum_gain_and_zero_deadzone()
    {
        var settings = new AppSettings();
        SensitivityPresets.Apply(settings, SensitivityLevel.HairTrigger);

        Assert.Equal(20.0, settings.Sensitivity);
        Assert.Equal(0, settings.DeadzonePercent);
    }
}

public class BalanceDisplayTests
{
    [Fact]
    public void GetCenterDotPercent_idle_below_threshold_returns_center_not_origin()
    {
        var idle = new ProcessedBalance
        {
            WeightKg = 0,
            BalanceX = 0,
            BalanceY = 0,
        };

        var (x, y) = BalanceDisplay.GetCenterDotPercent(idle);

        Assert.Equal(BalanceConstants.BalanceCenterPercent, x);
        Assert.Equal(BalanceConstants.BalanceCenterPercent, y);
        Assert.NotEqual(0f, x);
        Assert.NotEqual(0f, y);
    }

    [Fact]
    public void GetCenterDotPercent_phantom_origin_on_board_still_centers()
    {
        var phantom = new ProcessedBalance
        {
            WeightKg = 8,
            BalanceX = 0,
            BalanceY = 0,
        };

        var (x, y) = BalanceDisplay.GetCenterDotPercent(phantom);

        Assert.Equal(BalanceConstants.BalanceCenterPercent, x);
        Assert.Equal(BalanceConstants.BalanceCenterPercent, y);
    }

    [Fact]
    public void CenterDotCanvasPosition_centered_is_not_top_left()
    {
        var (left, top) = BalanceDisplay.CenterDotCanvasPosition(
            BalanceConstants.BalanceCenterPercent,
            BalanceConstants.BalanceCenterPercent,
            canvasWidth: 200,
            canvasHeight: 200,
            dotWidth: 22,
            dotHeight: 22);

        Assert.True(left > 0);
        Assert.True(top > 0);
        Assert.InRange(left, 85, 95);
        Assert.InRange(top, 85, 95);
    }

    [Fact]
    public void CenterDotCanvasPosition_origin_is_top_left()
    {
        var (left, top) = BalanceDisplay.CenterDotCanvasPosition(0, 0, 200, 200, 22, 22);
        Assert.Equal(0, left);
        Assert.Equal(0, top);
    }
}

public class ActionEngineTests
{
    [Fact]
    public void Apply_presses_key_for_active_slot()
    {
        var backend = new RecordingInputBackend();
        var engine = new ActionEngine(backend);
        var settings = new AppSettings { Actions = AppSettings.CreateDefaultActions() };
        settings.Actions[ActionSlots.Left] = new() { Kind = ActionKind.Key, KeyName = "A" };

        var data = new ProcessedBalance { MoveLeft = true };
        engine.Apply(data, settings);

        Assert.Contains(backend.Events, e => e.Kind == "keydown" && e.Vk == 0x41);
    }

    [Fact]
    public void Apply_presses_mouse_on_jump()
    {
        var backend = new RecordingInputBackend();
        var engine = new ActionEngine(backend);
        var settings = new AppSettings { Actions = AppSettings.CreateDefaultActions() };
        settings.Actions[ActionSlots.Jump] = new() { Kind = ActionKind.MouseButton, MouseButton = "Left" };

        engine.Apply(new ProcessedBalance { Jump = true }, settings);

        Assert.Contains(backend.Events, e => e.Kind == "mousedown");
    }

    [Fact]
    public void ReleaseAll_releases_held_keys()
    {
        var backend = new RecordingInputBackend();
        var engine = new ActionEngine(backend);
        var settings = new AppSettings { Actions = AppSettings.CreateDefaultActions() };
        settings.Actions[ActionSlots.Forward] = new() { Kind = ActionKind.Key, KeyName = "W" };

        engine.Apply(new ProcessedBalance { MoveForward = true }, settings);
        engine.ReleaseAll();

        Assert.Contains(backend.Events, e => e.Kind == "keyup" && e.Vk == 0x57);
    }

    private sealed class RecordingInputBackend : IInputBackend
    {
        public List<(string Kind, ushort Vk)> Events { get; } = [];

        public void KeyDown(ushort virtualKey) => Events.Add(("keydown", virtualKey));

        public void KeyUp(ushort virtualKey) => Events.Add(("keyup", virtualKey));

        public void MouseDown(string button) => Events.Add(("mousedown", 0));

        public void MouseUp(string button) => Events.Add(("mouseup", 0));

        public void MoveRelative(int deltaX, int deltaY) => Events.Add(("move", 0));
    }
}
