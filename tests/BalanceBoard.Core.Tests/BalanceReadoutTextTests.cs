using BalanceBoard.Core.Models;
using BalanceBoard.Core.Processing;
using Xunit;

namespace BalanceBoard.Core.Tests;

public class BalanceReadoutTextTests
{
    [Fact]
    public void DescribeDirection_jump_off_board_returns_jump_only()
    {
        var data = new ProcessedBalance { Jump = true, WeightKg = 0 };
        Assert.Equal("Jump!", BalanceReadoutText.DescribeDirection(data));
    }

    [Fact]
    public void DescribeDirection_jump_on_board_includes_lean()
    {
        var data = new ProcessedBalance
        {
            Jump = true,
            WeightKg = 10,
            MoveForward = true,
        };
        Assert.Equal("Jump! · forward", BalanceReadoutText.DescribeDirection(data));
    }

    [Fact]
    public void DescribeDirection_idle_prompts_step_on_board()
    {
        var data = new ProcessedBalance { WeightKg = 0 };
        Assert.Equal("Step on the board", BalanceReadoutText.DescribeDirection(data));
    }

    [Fact]
    public void DescribeDirection_centered_on_board()
    {
        var data = new ProcessedBalance { WeightKg = 10 };
        Assert.Equal("Centered", BalanceReadoutText.DescribeDirection(data));
    }

    [Fact]
    public void DescribeDirection_lean_forward()
    {
        var data = new ProcessedBalance { WeightKg = 10, MoveForward = true };
        Assert.Equal("forward", BalanceReadoutText.DescribeDirection(data));
    }

    [Fact]
    public void DescribeLean_combines_directions()
    {
        var data = new ProcessedBalance
        {
            MoveForward = true,
            MoveLeft = true,
        };
        Assert.Equal("forward · left", BalanceReadoutText.DescribeLean(data));
    }

    [Fact]
    public void DescribeActiveInputs_jump_off_board()
    {
        var data = new ProcessedBalance { Jump = true, WeightKg = 0 };
        Assert.Equal("Active: Jump", BalanceReadoutText.DescribeActiveInputs(data, new AppSettings()));
    }

    [Fact]
    public void DescribeActiveInputs_idle_shows_profile()
    {
        var settings = new AppSettings { ActiveProfileName = "Minecraft" };
        var data = new ProcessedBalance { WeightKg = 0 };
        Assert.Equal("Profile: Minecraft", BalanceReadoutText.DescribeActiveInputs(data, settings));
    }

    [Fact]
    public void DescribeActiveInputs_centered_on_board()
    {
        var data = new ProcessedBalance { WeightKg = 10 };
        Assert.Equal("Centered", BalanceReadoutText.DescribeActiveInputs(data, new AppSettings()));
    }

    [Fact]
    public void DescribeActiveInputs_lists_movements()
    {
        var data = new ProcessedBalance
        {
            WeightKg = 10,
            MoveForward = true,
            MoveRight = true,
        };
        Assert.Equal("Active: Forward, Right", BalanceReadoutText.DescribeActiveInputs(data, new AppSettings()));
    }

    [Fact]
    public void DescribeBoardButton_no_binding_pressed()
    {
        var settings = new AppSettings { Actions = new Dictionary<string, ActionBinding>() };
        var data = new ProcessedBalance { ButtonA = true };
        Assert.Equal("Board button: pressed (A)", BalanceReadoutText.DescribeBoardButton(data, settings));
    }

    [Fact]
    public void DescribeBoardButton_key_binding_pressed()
    {
        var settings = new AppSettings();
        settings.Actions[ActionSlots.BoardButton] = new ActionBinding
        {
            Kind = ActionKind.Key,
            KeyName = "Escape",
        };
        var data = new ProcessedBalance { ButtonA = true };
        Assert.Equal("Board button → Escape", BalanceReadoutText.DescribeBoardButton(data, settings));
    }

    [Fact]
    public void DescribeBoardButton_vjoy_binding_pressed()
    {
        var settings = new AppSettings();
        settings.Actions[ActionSlots.BoardButton] = new ActionBinding
        {
            Kind = ActionKind.VJoyButton,
            VJoyButtonNumber = 3,
        };
        var data = new ProcessedBalance { ButtonA = true };
        Assert.Equal("Board button → vJoy #3", BalanceReadoutText.DescribeBoardButton(data, settings));
    }

    [Fact]
    public void DescribeBoardButton_jump_maps_to_vjoy()
    {
        var settings = new AppSettings { MapJumpToVJoyButton = true, JumpVJoyButton = 2 };
        var data = new ProcessedBalance { Jump = true };
        Assert.Equal("Jump → vJoy #2", BalanceReadoutText.DescribeBoardButton(data, settings));
    }

    [Fact]
    public void DescribeBoardButton_up()
    {
        var settings = new AppSettings();
        var data = new ProcessedBalance();
        Assert.Equal("Board button: up", BalanceReadoutText.DescribeBoardButton(data, settings));
    }
}
