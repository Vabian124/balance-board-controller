using BalanceBoard.Core.Abstractions;
using BalanceBoard.Core.Models;
using BalanceBoard.Core.Processing;
using BalanceBoard.Core.Services;
using Xunit;

namespace BalanceBoard.Core.Tests;

public class BalanceMathTests
{
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
        var stillJumping = BalanceMath.EvaluateJump(0.5f, DateTime.UtcNow, ref jumpTime);
        Assert.True(stillJumping);
    }

    [Fact]
    public void EvaluateJump_resets_when_weight_returns()
    {
        var jumpTime = DateTime.UtcNow.AddSeconds(-5);
        var jumped = BalanceMath.EvaluateJump(60f, DateTime.UtcNow, ref jumpTime);
        Assert.False(jumped);
        Assert.True(jumpTime > DateTime.UtcNow.AddSeconds(-1));
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
