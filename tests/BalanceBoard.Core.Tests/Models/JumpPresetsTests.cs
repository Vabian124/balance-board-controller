using BalanceBoard.Core.Models;
using BalanceBoard.Core.Processing;
using Xunit;

namespace BalanceBoard.Core.Tests;

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
    public void CreateFresh_uses_minecraft_keyboard_template()
    {
        var settings = AppSettings.CreateFresh();
        Assert.Equal(ActionPresets.Minecraft, settings.ActiveProfileName);
        Assert.False(settings.DisableKeyboardActions);
        Assert.Equal("Space", settings.Actions[ActionSlots.Jump].KeyName);
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
        ActionPresets.ApplyMinecraftControlify(settings);
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
