using BalanceBoard.Core.Models;
using BalanceBoard.Core.Processing;
using Xunit;

namespace BalanceBoard.Core.Tests;

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
