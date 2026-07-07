using System.Text.Json;
using BalanceBoard.Core.Models;

namespace BalanceBoard.Core.Services;

public sealed class SettingsStore
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public SettingsStore(string? baseDirectory = null)
    {
        if (baseDirectory is not null)
        {
            Directory.CreateDirectory(baseDirectory);
            SettingsPath = Path.Combine(baseDirectory, "settings.json");
        }
        else
        {
            AppDataPaths.EnsureRoot();
            SettingsPath = AppDataPaths.SettingsFile;
        }
    }

    public string SettingsPath { get; }

    public bool HasPersistedSettings => File.Exists(SettingsPath);

    public AppSettings Load()
    {
        if (!File.Exists(SettingsPath))
        {
            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            if (ApplyMigrations(settings, json))
            {
                Save(settings);
            }

            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, _jsonOptions);
        var directory = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(directory);
        var tempPath = SettingsPath + ".tmp";

        File.WriteAllText(tempPath, json);
        File.Move(tempPath, SettingsPath, overwrite: true);
    }

    public void UpdateConnectionState(AppSettings settings, string? deviceId)
    {
        if (!DeviceIdRules.ShouldPersistConnectionState(deviceId))
        {
            return;
        }

        settings.HasConnectedBefore = true;
        settings.SetupWizardCompleted = true;
        settings.LastConnectedDeviceId = deviceId;
        settings.LastConnectedAtUtc = DateTime.UtcNow;
        Save(settings);
    }

    public string ProfilesDirectory
    {
        get
        {
            var dir = Path.Combine(Path.GetDirectoryName(SettingsPath)!, "profiles");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public void SaveProfile(string name, AppSettings settings)
    {
        var safeName = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
        var path = Path.Combine(ProfilesDirectory, $"{safeName}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(settings, _jsonOptions));
    }

    public IReadOnlyList<string> ListProfiles()
    {
        return [.. Directory.GetFiles(ProfilesDirectory, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => n is not null)
            .Cast<string>()
            .OrderBy(n => n)];
    }

    public AppSettings? LoadProfile(string name)
    {
        var safeName = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
        var path = Path.Combine(ProfilesDirectory, $"{safeName}.json");
        if (!File.Exists(path))
        {
            return null;
        }

        return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path));
    }

    private static bool ApplyMigrations(AppSettings settings, string rawJson)
    {
        var changed = false;

        if (!settings.HasConnectedBefore && settings.SetupWizardCompleted)
        {
            settings.HasConnectedBefore = true;
            changed = true;
        }

        // Older builds never wrote HasConnectedBefore — persist it when wizard was completed.
        if (settings.HasConnectedBefore && !rawJson.Contains("HasConnectedBefore", StringComparison.Ordinal))
        {
            changed = true;
        }

        if (DeviceIdRules.IsSimulated(settings.LastConnectedDeviceId))
        {
            settings.LastConnectedDeviceId = null;
            settings.LastConnectedAtUtc = null;
            changed = true;
        }

        foreach (var slot in ActionSlots.All)
        {
            if (!settings.Actions.ContainsKey(slot))
            {
                settings.Actions[slot] = new ActionBinding();
                changed = true;
            }
        }

        return changed;
    }
}
