using System.Text.Json;
using BalanceBoard.Core.Models;
using BalanceBoard.Core.Services;
using Xunit;

namespace BalanceBoard.Core.Tests;

public class DeviceIdRulesTests
{
    [Theory]
    [InlineData("SIM-BOARD-001", true)]
    [InlineData("sim-board-002", true)]
    [InlineData("HID\\VID_057E&PID_0306", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void IsSimulated_recognizes_sim_prefix(string? id, bool expected) =>
        Assert.Equal(expected, DeviceIdRules.IsSimulated(id));

    [Theory]
    [InlineData("SIM-BOARD-001", false)]
    [InlineData("HID\\VID_057E", true)]
    public void ShouldPersistConnectionState_excludes_simulated(string id, bool expected) =>
        Assert.Equal(expected, DeviceIdRules.ShouldPersistConnectionState(id));

    [Fact]
    public void ExtractFromHidPath_parses_device_id_segment()
    {
        const string path = @"\\?\hid#vid_057e&pid_0306&col01#e_pid&0001a2b3c4d5&0&0000{00000000-0000-0000-0000-000000000000}";
        Assert.Equal("0001A2B3C4D5", DeviceIdRules.ExtractFromHidPath(path));
    }
}

public class SettingsStoreTests
{
    [Fact]
    public void UpdateConnectionState_ignores_simulated_device_id()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bb-tests", Guid.NewGuid().ToString("N"));
        var store = new SettingsStore(dir);
        var settings = new AppSettings();

        store.UpdateConnectionState(settings, DeviceIdRules.SimulatedDeviceId);

        Assert.False(settings.HasConnectedBefore);
        Assert.Null(settings.LastConnectedDeviceId);
        Assert.False(File.Exists(store.SettingsPath));
    }

    [Fact]
    public void Load_clears_simulated_last_board_from_older_settings()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bb-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "settings.json");
        var json = JsonSerializer.Serialize(new
        {
            LastConnectedDeviceId = DeviceIdRules.SimulatedDeviceId,
            HasConnectedBefore = true,
        });
        File.WriteAllText(path, json);

        var store = new SettingsStore(dir);
        var settings = store.Load();

        Assert.Null(settings.LastConnectedDeviceId);
        Assert.Null(settings.LastConnectedAtUtc);
    }
}
