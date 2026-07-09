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
    [InlineData(OutputMode.VirtualController, VirtualControllerBackend.VJoy, true, true)]
    [InlineData(OutputMode.VirtualController, VirtualControllerBackend.Xbox360, false, true)]
    [InlineData(OutputMode.Keyboard, VirtualControllerBackend.VJoy, false, false)]
    public void SetOutputMode_syncs_legacy_flags(
        OutputMode mode,
        VirtualControllerBackend backend,
        bool enableVJoy,
        bool disableKeyboard)
    {
        var settings = new AppSettings();
        settings.SetVirtualControllerBackend(backend);
        settings.SetOutputMode(mode);
        Assert.Equal(mode, settings.OutputMode);
        Assert.Equal(backend, settings.VirtualControllerBackend);
        Assert.Equal(enableVJoy, settings.EnableVJoy);
        Assert.Equal(disableKeyboard, settings.DisableKeyboardActions);
    }

    [Fact]
    public void SetVirtualControllerBackend_respects_current_output_mode()
    {
        var settings = new AppSettings();
        settings.SetOutputMode(OutputMode.VirtualController);
        settings.SetVirtualControllerBackend(VirtualControllerBackend.Xbox360);

        Assert.Equal(VirtualControllerBackend.Xbox360, settings.VirtualControllerBackend);
        Assert.False(settings.EnableVJoy);
        Assert.True(settings.DisableKeyboardActions);
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
        settings.SetVirtualControllerBackend(VirtualControllerBackend.VJoy);
        settings.SetOutputMode(OutputMode.VirtualController);
        settings.MapJumpToVJoyButton = true;
        settings.JumpVJoyButton = 3;
        _store.Save(settings);
        var loaded = _store.Load();
        Assert.Equal(OutputMode.VirtualController, loaded.OutputMode);
        Assert.Equal(VirtualControllerBackend.VJoy, loaded.VirtualControllerBackend);
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

    [Fact]
    public void Save_and_load_round_trips_Xbox360_virtual_controller_placeholder()
    {
        var settings = new AppSettings();
        settings.SetVirtualControllerBackend(VirtualControllerBackend.Xbox360);
        settings.SetOutputMode(OutputMode.VirtualController);
        _store.Save(settings);

        var loaded = _store.Load();

        Assert.Equal(OutputMode.VirtualController, loaded.OutputMode);
        Assert.Equal(VirtualControllerBackend.Xbox360, loaded.VirtualControllerBackend);
        Assert.False(loaded.EnableVJoy);
        Assert.True(loaded.DisableKeyboardActions);
    }
}
