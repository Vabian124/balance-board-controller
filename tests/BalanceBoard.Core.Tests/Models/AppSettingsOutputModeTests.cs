using BalanceBoard.Core.Models;
using BalanceBoard.Core.Services;
using Xunit;

namespace BalanceBoard.Core.Tests.Models;

public class AppSettingsOutputModeTests : IDisposable
{
    private readonly string _dir;
    private readonly SettingsStore _store;

    public AppSettingsOutputModeTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "bbctl-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _store = new SettingsStore(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    [Theory]
    [InlineData(OutputMode.VJoy, true, true)]
    [InlineData(OutputMode.Keyboard, false, false)]
    public void SetOutputMode_syncs_legacy_flags(OutputMode mode, bool enableVJoy, bool disableKeyboard)
    {
        var settings = new AppSettings();
        settings.SetOutputMode(mode);
        Assert.Equal(mode, settings.OutputMode);
        Assert.Equal(enableVJoy, settings.EnableVJoy);
        Assert.Equal(disableKeyboard, settings.DisableKeyboardActions);
    }

    [Fact]
    public void SetOutputMode_Keyboard_clears_MapJumpToVJoyButton()
    {
        var settings = new AppSettings { MapJumpToVJoyButton = true };
        settings.SetOutputMode(OutputMode.Keyboard);
        Assert.False(settings.MapJumpToVJoyButton);
    }

    [Fact]
    public void Save_and_load_round_trips_OutputMode()
    {
        var settings = new AppSettings();
        settings.SetOutputMode(OutputMode.VJoy);
        settings.MapJumpToVJoyButton = true;
        settings.JumpVJoyButton = 3;
        _store.Save(settings);
        var loaded = _store.Load();
        Assert.Equal(OutputMode.VJoy, loaded.OutputMode);
        Assert.True(loaded.EnableVJoy);
        Assert.True(loaded.DisableKeyboardActions);
        Assert.True(loaded.MapJumpToVJoyButton);
        Assert.Equal(3, loaded.JumpVJoyButton);
    }

    [Fact]
    public void Save_and_load_round_trips_Keyboard_OutputMode()
    {
        var settings = new AppSettings();
        settings.SetOutputMode(OutputMode.Keyboard);
        _store.Save(settings);
        var loaded = _store.Load();
        Assert.Equal(OutputMode.Keyboard, loaded.OutputMode);
        Assert.False(loaded.EnableVJoy);
        Assert.False(loaded.DisableKeyboardActions);
        Assert.False(loaded.MapJumpToVJoyButton);
    }
}
