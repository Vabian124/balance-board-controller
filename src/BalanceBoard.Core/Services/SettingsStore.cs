using System.Runtime.InteropServices;
using System.Text.Json;
using BalanceBoard.Core.Models;

namespace BalanceBoard.Core.Services;

public sealed class SettingsStore
{
    private readonly string _settingsPath;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public SettingsStore(string? baseDirectory = null)
    {
        var root = baseDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BalanceBoardApp");
        Directory.CreateDirectory(root);
        _settingsPath = Path.Combine(root, "settings.json");
    }

    public AppSettings Load()
    {
        if (!File.Exists(_settingsPath))
        {
            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(_settingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, _jsonOptions);
        File.WriteAllText(_settingsPath, json);
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
}
