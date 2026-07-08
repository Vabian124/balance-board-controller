using BalanceBoard.Core.Models;
using BalanceBoard.Core.Processing;
using Xunit;

namespace BalanceBoard.Core.Tests;

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
