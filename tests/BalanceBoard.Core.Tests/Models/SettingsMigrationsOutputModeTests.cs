using System.Text.Json;
using BalanceBoard.Core.Models;
using BalanceBoard.Core.Services.Settings;
using Xunit;

namespace BalanceBoard.Core.Tests.Models;

public class SettingsMigrationsOutputModeTests : IDisposable
{
    private readonly string _dir;
    private readonly SettingsStore _store;

    public SettingsMigrationsOutputModeTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "bbctl-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _store = new SettingsStore(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    [Fact]
    public void Load_json_without_OutputMode_infers_VJoy_from_legacy_flags()
    {
        var json = JsonSerializer.Serialize(new { EnableVJoy = true, DisableKeyboardActions = true });
        File.WriteAllText(_store.SettingsPath, json);
        var loaded = _store.Load();
        Assert.Equal(OutputMode.VirtualController, loaded.OutputMode);
        Assert.Equal(VirtualControllerBackend.VJoy, loaded.VirtualControllerBackend);
        Assert.True(loaded.EnableVJoy);
        Assert.True(loaded.DisableKeyboardActions);
        Assert.Contains("\"OutputMode\"", File.ReadAllText(_store.SettingsPath));
        Assert.Contains("\"VirtualControllerBackend\"", File.ReadAllText(_store.SettingsPath));
    }

    [Fact]
    public void Load_json_without_OutputMode_infers_Keyboard_when_movement_keys_enabled()
    {
        var json = JsonSerializer.Serialize(new { EnableVJoy = false, DisableKeyboardActions = false });
        File.WriteAllText(_store.SettingsPath, json);
        var loaded = _store.Load();
        Assert.Equal(OutputMode.Keyboard, loaded.OutputMode);
        Assert.Equal(VirtualControllerBackend.VJoy, loaded.VirtualControllerBackend);
        Assert.False(loaded.EnableVJoy);
        Assert.False(loaded.DisableKeyboardActions);
    }

    [Fact]
    public void Load_json_without_OutputMode_infers_Keyboard_for_mixed_legacy_flags()
    {
        var json = JsonSerializer.Serialize(new { EnableVJoy = true, DisableKeyboardActions = false });
        File.WriteAllText(_store.SettingsPath, json);
        var loaded = _store.Load();
        Assert.Equal(OutputMode.Keyboard, loaded.OutputMode);
    }
}
