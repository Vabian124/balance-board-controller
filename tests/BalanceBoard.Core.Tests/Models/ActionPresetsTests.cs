using BalanceBoard.Core.Models;
using BalanceBoard.Core.Processing;
using Xunit;

namespace BalanceBoard.Core.Tests;

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
    public void ApplyMinecraft_binds_wasd_and_space_jump()
    {
        var settings = new AppSettings();
        ActionPresets.ApplyMinecraft(settings);
        Assert.Equal(ActionPresets.Minecraft, settings.ActiveProfileName);
        Assert.False(settings.EnableVJoy);
        Assert.False(settings.DisableKeyboardActions);
        Assert.False(settings.MapJumpToVJoyButton);
        Assert.Equal("W", settings.Actions[ActionSlots.Forward].KeyName);
        Assert.Equal("S", settings.Actions[ActionSlots.Backward].KeyName);
        Assert.Equal("A", settings.Actions[ActionSlots.Left].KeyName);
        Assert.Equal("D", settings.Actions[ActionSlots.Right].KeyName);
        Assert.Equal("Space", settings.Actions[ActionSlots.Jump].KeyName);
        Assert.Equal(BalanceConstants.JumpNormalThresholdKg, settings.JumpWeightThresholdKg);
        Assert.Equal(ResponseCurve.MinecraftSnappy, settings.ResponseCurve);
    }

    [Fact]
    public void ApplyMinecraftControlify_maps_jump_to_vjoy_and_move_axes()
    {
        var settings = new AppSettings();
        ActionPresets.ApplyMinecraftControlify(settings);
        Assert.Equal(ActionPresets.MinecraftControlify, settings.ActiveProfileName);
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
    public void ApplyMinecraftControlify_uses_normal_jump_preset()
    {
        var settings = new AppSettings();
        ActionPresets.ApplyMinecraftControlify(settings);
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
