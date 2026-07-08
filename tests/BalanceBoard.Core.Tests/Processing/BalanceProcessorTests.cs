using BalanceBoard.Core.Models;
using BalanceBoard.Core.Processing;
using Xunit;

namespace BalanceBoard.Core.Tests.Processing;

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
        Assert.False(jumped.VJoyButton1);
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
