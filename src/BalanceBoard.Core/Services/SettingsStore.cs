using System.Text.Json;
using BalanceBoard.Core.Models;

namespace BalanceBoard.Core.Services;

public sealed class SettingsStore
{
    private readonly string _settingsPath;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public SettingsStore(string? baseDirectory = null)
    {
        if (baseDirectory is not null)
        {
            Directory.CreateDirectory(baseDirectory);
            _settingsPath = Path.Combine(baseDirectory, "settings.json");
        }
        else
        {
            AppDataPaths.EnsureRoot();
            _settingsPath = AppDataPaths.SettingsFile;
        }
    }

    public string SettingsPath => _settingsPath;

    public bool HasPersistedSettings => File.Exists(_settingsPath);

    public AppSettings Load()
    {
        if (!File.Exists(_settingsPath))
        {
            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(_settingsPath);
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
        var directory = Path.GetDirectoryName(_settingsPath)!;
        Directory.CreateDirectory(directory);
        var tempPath = _settingsPath + ".tmp";

        File.WriteAllText(tempPath, json);
        File.Move(tempPath, _settingsPath, overwrite: true);
    }

    public void UpdateConnectionState(AppSettings settings, string? deviceId)
    {
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
            var dir = Path.Combine(Path.GetDirectoryName(_settingsPath)!, "profiles");
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
        return Directory.GetFiles(ProfilesDirectory, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => n is not null)
            .Cast<string>()
            .OrderBy(n => n)
            .ToList();
    }

    public AppSettings? LoadProfile(string name)
    {
        var safeName = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
        var path = Path.Combine(ProfilesDirectory, $"{safeName}.json");
        if (!File.Exists(path)) return null;
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

        return changed;
    }
}
