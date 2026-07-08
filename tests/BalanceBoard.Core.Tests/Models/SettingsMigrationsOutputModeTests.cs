using BalanceBoard.Core.Models;
using BalanceBoard.Core.Services;
using Xunit;

namespace BalanceBoard.Core.Tests;

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
        var json = """
            {
              "EnableVJoy": true,
              "DisableKeyboardActions": true
            }
            """;
        File.WriteAllText(_store.SettingsPath, json);
        var loaded = _store.Load();
        Assert.Equal(OutputMode.VJoy, loaded.OutputMode);
        Assert.True(loaded.EnableVJoy);
        Assert.True(loaded.DisableKeyboardActions);
        Assert.Contains("\"OutputMode\"", File.ReadAllText(_store.SettingsPath));
    }

    [Fact]
    public void Load_json_without_OutputMode_infers_Keyboard_when_movement_keys_enabled()
    {
        var json = """
            {
              "EnableVJoy": false,
              "DisableKeyboardActions": false
            }
            """;
        File.WriteAllText(_store.SettingsPath, json);
        var loaded = _store.Load();
        Assert.Equal(OutputMode.Keyboard, loaded.OutputMode);
        Assert.False(loaded.EnableVJoy);
        Assert.False(loaded.DisableKeyboardActions);
    }

    [Fact]
    public void Load_json_without_OutputMode_infers_Keyboard_for_mixed_legacy_flags()
    {
        var json = """
            {
              "EnableVJoy": true,
              "DisableKeyboardActions": false
            }
            """;
        File.WriteAllText(_store.SettingsPath, json);
        var loaded = _store.Load();
        Assert.Equal(OutputMode.Keyboard, loaded.OutputMode);
    }
}
