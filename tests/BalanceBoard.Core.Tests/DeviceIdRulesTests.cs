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

    [Fact]
    public void ExtractFromHidPath_uses_instance_segment_not_product_id()
    {
        const string path = @"\\?\hid#vid_057e&pid_0306&col01#e_pid&0306&37a15347&0&0000{00000000-0000-0000-0000-000000000000}";
        Assert.Equal("37A15347", DeviceIdRules.ExtractFromHidPath(path));
    }

    [Fact]
    public void ExtractFromHidPath_parses_windows_hid_instance_segment()
    {
        const string path =
            @"\\?\hid#{00001124-0000-1000-8000-00805f9b34fb}_vid&0002057e_pid&0306#9&37a15347&9&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}";
        Assert.Equal("37A15347", DeviceIdRules.ExtractFromHidPath(path));
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
    public void Save_default_settings_does_not_throw()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bb-tests", Guid.NewGuid().ToString("N"));
        var store = new SettingsStore(dir);

        store.Save(new AppSettings());

        Assert.True(File.Exists(store.SettingsPath));
    }

    [Fact]
    public void Save_and_Load_roundtrips_per_axis_deadzone()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bb-tests", Guid.NewGuid().ToString("N"));
        var store = new SettingsStore(dir);

        store.Save(new AppSettings { DeadzonePercent = 8 });
        var loaded = store.Load();
        Assert.Null(loaded.DeadzoneLeftRightPercent);
        Assert.Null(loaded.DeadzoneForwardBackwardPercent);

        store.Save(new AppSettings
        {
            DeadzonePercent = 8,
            DeadzoneLeftRightPercent = 3,
            DeadzoneForwardBackwardPercent = 12,
        });
        loaded = store.Load();

        Assert.Equal(3, loaded.DeadzoneLeftRightPercent);
        Assert.Equal(12, loaded.DeadzoneForwardBackwardPercent);
    }

    [Fact]
    public void Save_and_Load_roundtrips_per_axis_sensitivity()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bb-tests", Guid.NewGuid().ToString("N"));
        var store = new SettingsStore(dir);

        store.Save(new AppSettings { Sensitivity = 1.5 });
        var loaded = store.Load();
        Assert.Null(loaded.SensitivityLeftRight);
        Assert.Null(loaded.SensitivityForwardBackward);

        store.Save(new AppSettings
        {
            Sensitivity = 1.5,
            SensitivityLeftRight = 3,
            SensitivityForwardBackward = 12,
        });
        loaded = store.Load();

        Assert.Equal(3, loaded.SensitivityLeftRight);
        Assert.Equal(12, loaded.SensitivityForwardBackward);
    }

    [Fact]
    public void Save_and_Load_roundtrips_settings_fields()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bb-tests", Guid.NewGuid().ToString("N"));
        var store = new SettingsStore(dir);
        var settings = new AppSettings
        {
            DeadzonePercent = 12,
            Sensitivity = 1.5,
            ActiveProfileName = "TestProfile",
            JumpWeightThresholdKg = 33f,
            JumpLevel = JumpLevel.Easy,
            UiDetailLevel = UiDetailLevel.Advanced,
            ResponseCurve = ResponseCurve.MinecraftSnappy,
            OneFootMode = true,
        };
        settings.SetVirtualControllerBackend(VirtualControllerBackend.VJoy);
        settings.SetOutputMode(OutputMode.VirtualController);

        store.Save(settings);
        var loaded = store.Load();

        Assert.Equal(settings.DeadzonePercent, loaded.DeadzonePercent);
        Assert.Equal(settings.Sensitivity, loaded.Sensitivity);
        Assert.Equal(settings.ActiveProfileName, loaded.ActiveProfileName);
        Assert.Equal(settings.JumpWeightThresholdKg, loaded.JumpWeightThresholdKg);
        Assert.Equal(settings.JumpLevel, loaded.JumpLevel);
        Assert.Equal(settings.UiDetailLevel, loaded.UiDetailLevel);
        Assert.Equal(settings.EnableVJoy, loaded.EnableVJoy);
        Assert.Equal(settings.ResponseCurve, loaded.ResponseCurve);
        Assert.Equal(settings.OneFootMode, loaded.OneFootMode);
        Assert.True(File.Exists(store.SettingsPath));
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
