using BalanceBoard.Core.Models;
using BalanceBoard.Core.Services;
using Xunit;

namespace BalanceBoard.Core.Tests;

public class SettingsStoreProfileTests : IDisposable
{
    private readonly string _dir;
    private readonly SettingsStore _store;

    public SettingsStoreProfileTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "bbctl-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _store = new SettingsStore(_dir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch
        {
            // best-effort temp cleanup
        }
    }

    [Fact]
    public async Task Concurrent_Save_and_Load_do_not_throw_or_corrupt()
    {
        var settings = new AppSettings { Sensitivity = 2.5 };
        _store.Save(settings);

        Exception? writeError = null;
        Exception? readError = null;
        var barrier = new Barrier(2);

        var writer = Task.Run(() =>
        {
            barrier.SignalAndWait();
            try
            {
                for (var i = 0; i < 40; i++)
                {
                    settings.Sensitivity = 1.0 + (i % 5) * 0.1;
                    _store.Save(settings);
                }
            }
            catch (Exception ex)
            {
                writeError = ex;
            }
        });

        var reader = Task.Run(() =>
        {
            barrier.SignalAndWait();
            try
            {
                for (var i = 0; i < 40; i++)
                {
                    _ = _store.Load();
                }
            }
            catch (Exception ex)
            {
                readError = ex;
            }
        });

        await Task.WhenAll(writer, reader);
        Assert.Null(writeError);
        Assert.Null(readError);
        Assert.True(File.Exists(_store.SettingsPath));
        Assert.NotNull(_store.Load());
    }

    [Fact]
    public void SaveProfile_then_LoadProfile_round_trips_tuning()
    {
        var settings = new AppSettings { Sensitivity = 7.5, DeadzonePercent = 12, InvertX = true };
        settings.Actions[ActionSlots.Jump] = new ActionBinding { Kind = ActionKind.Key, KeyName = "Space" };

        _store.SaveProfile("My Game", settings);

        Assert.Contains("My Game", _store.ListProfiles());
        var loaded = _store.LoadProfile("My Game");
        Assert.NotNull(loaded);
        Assert.Equal(7.5, loaded!.Sensitivity);
        Assert.Equal(12, loaded.DeadzonePercent);
        Assert.True(loaded.InvertX);
        Assert.Equal(ActionKind.Key, loaded.Actions[ActionSlots.Jump].Kind);
        Assert.Equal("Space", loaded.Actions[ActionSlots.Jump].KeyName);
    }

    [Fact]
    public void SaveProfile_strips_connection_identity()
    {
        var settings = new AppSettings
        {
            HasConnectedBefore = true,
            SetupWizardCompleted = true,
            LastConnectedDeviceId = "0001A2B3C4D5",
            LastConnectedAtUtc = DateTime.UtcNow,
            LastBluetoothAdapterMac = "AABBCCDDEEFF",
        };

        _store.SaveProfile("Shared", settings);
        var loaded = _store.LoadProfile("Shared");

        Assert.NotNull(loaded);
        Assert.False(loaded!.HasConnectedBefore);
        Assert.False(loaded.SetupWizardCompleted);
        Assert.Null(loaded.LastConnectedDeviceId);
        Assert.Null(loaded.LastConnectedAtUtc);
        Assert.Null(loaded.LastBluetoothAdapterMac);
    }

    [Fact]
    public void LoadProfile_returns_null_for_missing_profile() =>
        Assert.Null(_store.LoadProfile("does-not-exist"));

    [Fact]
    public void DeleteProfile_removes_file_and_reports_result()
    {
        _store.SaveProfile("Temp", new AppSettings());
        Assert.True(_store.ProfileExists("Temp"));

        Assert.True(_store.DeleteProfile("Temp"));
        Assert.False(_store.ProfileExists("Temp"));
        Assert.DoesNotContain("Temp", _store.ListProfiles());
        Assert.False(_store.DeleteProfile("Temp"));
    }

    [Fact]
    public void LoadProfile_backfills_missing_action_slots()
    {
        _store.SaveProfile("Partial", new AppSettings());
        var loaded = _store.LoadProfile("Partial");

        Assert.NotNull(loaded);
        foreach (var slot in ActionSlots.All)
        {
            Assert.True(loaded!.Actions.ContainsKey(slot));
        }
    }

    [Fact]
    public void ExportSettings_then_ImportSettings_round_trips()
    {
        var settings = new AppSettings { Sensitivity = 3.5, JumpWeightThresholdKg = 22 };
        var path = Path.Combine(_dir, "exported.bbprofile.json");

        _store.ExportSettings(settings, path);
        Assert.True(File.Exists(path));

        var imported = _store.ImportSettings(path);
        Assert.NotNull(imported);
        Assert.Equal(3.5, imported!.Sensitivity);
        Assert.Equal(22, imported.JumpWeightThresholdKg);
    }

    [Fact]
    public void ImportSettings_returns_null_for_garbage_file()
    {
        var path = Path.Combine(_dir, "junk.json");
        File.WriteAllText(path, "not valid json {{{");
        Assert.Null(_store.ImportSettings(path));
    }

    [Fact]
    public void SanitizeProfileName_strips_invalid_characters()
    {
        var settings = new AppSettings { Sensitivity = 9 };
        _store.SaveProfile("bad/name:here", settings);

        // Sanitized name is what appears in the list and can be reloaded.
        var listed = _store.ListProfiles();
        Assert.Single(listed);
        var loaded = _store.LoadProfile(listed[0]);
        Assert.NotNull(loaded);
        Assert.Equal(9, loaded!.Sensitivity);
    }
}

public class AppSettingsCopyTests
{
    [Fact]
    public void CopyFrom_copies_tuning_but_preserves_connection_state_by_default()
    {
        var target = new AppSettings
        {
            HasConnectedBefore = true,
            LastConnectedDeviceId = "0001A2B3C4D5",
        };
        var source = new AppSettings
        {
            Sensitivity = 15,
            HasConnectedBefore = false,
            LastConnectedDeviceId = "SHOULD-NOT-COPY",
        };

        target.CopyFrom(source);

        Assert.Equal(15, target.Sensitivity);
        Assert.True(target.HasConnectedBefore);
        Assert.Equal("0001A2B3C4D5", target.LastConnectedDeviceId);
    }

    [Fact]
    public void CopyFrom_deep_copies_actions()
    {
        var source = new AppSettings();
        source.Actions[ActionSlots.Left] = new ActionBinding { Kind = ActionKind.Key, KeyName = "A" };
        var target = new AppSettings();

        target.CopyFrom(source);
        source.Actions[ActionSlots.Left].KeyName = "MUTATED";

        Assert.Equal("A", target.Actions[ActionSlots.Left].KeyName);
    }

    [Fact]
    public void Clone_produces_independent_full_copy()
    {
        var settings = new AppSettings { Sensitivity = 4, HasConnectedBefore = true };
        var clone = settings.Clone();

        Assert.Equal(4, clone.Sensitivity);
        Assert.True(clone.HasConnectedBefore);

        clone.Sensitivity = 99;
        Assert.Equal(4, settings.Sensitivity);
    }
}
