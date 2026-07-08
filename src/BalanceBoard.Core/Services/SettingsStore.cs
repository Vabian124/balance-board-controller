using System.Text.Json;
using BalanceBoard.Core.Models;

namespace BalanceBoard.Core.Services;

public sealed class SettingsStore
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private readonly object _ioLock = new();

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

    public bool HasPersistedSettings
    {
        get
        {
            lock (_ioLock)
            {
                return File.Exists(SettingsPath);
            }
        }
    }

    public AppSettings Load()
    {
        lock (_ioLock)
        {
            if (!File.Exists(SettingsPath))
            {
                return AppSettings.CreateFresh();
            }

            try
            {
                var json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? AppSettings.CreateFresh();
                if (ApplyMigrations(settings, json))
                {
                    SaveUnlocked(settings);
                }

                return settings;
            }
            catch
            {
                return AppSettings.CreateFresh();
            }
        }
    }

    public void Save(AppSettings settings)
    {
        lock (_ioLock)
        {
            SaveUnlocked(settings);
        }
    }

    private void SaveUnlocked(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, _jsonOptions);
        var directory = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(directory);
        var tempPath = SettingsPath + ".tmp";

        File.WriteAllText(tempPath, json);
        File.Move(tempPath, SettingsPath, overwrite: true);
    }

    public void UpdateConnectionState(AppSettings settings, string? deviceId, string? bluetoothAdapterMac = null)
    {
        if (!DeviceIdRules.ShouldPersistConnectionState(deviceId))
        {
            return;
        }

        settings.HasConnectedBefore = true;
        settings.SetupWizardCompleted = true;
        settings.LastConnectedDeviceId = deviceId;
        settings.LastConnectedAtUtc = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(bluetoothAdapterMac))
        {
            settings.LastBluetoothAdapterMac = bluetoothAdapterMac;
        }

        Save(settings);
    }

    public string ProfilesDirectory
    {
        get
        {
            lock (_ioLock)
            {
                return ProfilesDirectoryUnlocked();
            }
        }
    }

    /// <summary>Turn an arbitrary user string into a safe file name (no path separators / invalid chars).</summary>
    public static string SanitizeProfileName(string name) =>
        string.Join("_", (name ?? string.Empty).Split(Path.GetInvalidFileNameChars())).Trim();

    /// <summary>Persist a named profile snapshot. Connection identity is stripped so profiles are portable.</summary>
    public void SaveProfile(string name, AppSettings settings)
    {
        var snapshot = settings.Clone();
        snapshot.ClearConnectionState();
        lock (_ioLock)
        {
            File.WriteAllText(ProfilePath(name), JsonSerializer.Serialize(snapshot, _jsonOptions));
        }
    }

    public IReadOnlyList<string> ListProfiles()
    {
        lock (_ioLock)
        {
            return [.. Directory.GetFiles(ProfilesDirectoryUnlocked(), "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(n => n is not null)
                .Cast<string>()
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)];
        }
    }

    public bool ProfileExists(string name)
    {
        lock (_ioLock)
        {
            return File.Exists(ProfilePath(name));
        }
    }

    public AppSettings? LoadProfile(string name)
    {
        string? json;
        lock (_ioLock)
        {
            var path = ProfilePath(name);
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                json = File.ReadAllText(path);
            }
            catch
            {
                return null;
            }
        }

        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(json);
            if (settings is not null)
            {
                NormalizeLoadedProfile(settings);
            }

            return settings;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Delete a named profile. Returns true when a file was removed.</summary>
    public bool DeleteProfile(string name)
    {
        lock (_ioLock)
        {
            var path = ProfilePath(name);
            if (!File.Exists(path))
            {
                return false;
            }

            File.Delete(path);
            return true;
        }
    }

    /// <summary>Write a portable snapshot of <paramref name="settings"/> to an arbitrary path (Export…).</summary>
    public void ExportSettings(AppSettings settings, string destinationPath)
    {
        var snapshot = settings.Clone();
        snapshot.ClearConnectionState();
        var json = JsonSerializer.Serialize(snapshot, _jsonOptions);
        lock (_ioLock)
        {
            var directory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(destinationPath, json);
        }
    }

    /// <summary>Read a settings snapshot from an arbitrary path (Import…). Returns null on any failure.</summary>
    public AppSettings? ImportSettings(string sourcePath)
    {
        string? json;
        lock (_ioLock)
        {
            if (!File.Exists(sourcePath))
            {
                return null;
            }

            try
            {
                json = File.ReadAllText(sourcePath);
            }
            catch
            {
                return null;
            }
        }

        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(json);
            if (settings is not null)
            {
                NormalizeLoadedProfile(settings);
            }

            return settings;
        }
        catch
        {
            return null;
        }
    }

    private string ProfilesDirectoryUnlocked()
    {
        var dir = Path.Combine(Path.GetDirectoryName(SettingsPath)!, "profiles");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private string ProfilePath(string name) =>
        Path.Combine(ProfilesDirectoryUnlocked(), $"{SanitizeProfileName(name)}.json");

    /// <summary>Ensure a profile/imported snapshot has every action slot and no stray connection identity.</summary>
    private static void NormalizeLoadedProfile(AppSettings settings)
    {
        settings.ClearConnectionState();
        settings.Actions ??= AppSettings.CreateDefaultActions();
        foreach (var slot in ActionSlots.All)
        {
            if (!settings.Actions.ContainsKey(slot))
            {
                settings.Actions[slot] = new ActionBinding();
            }
        }
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

        // Older builds saved the Wii product id (0306) instead of the unique instance id.
        if (string.Equals(settings.LastConnectedDeviceId, "0306", StringComparison.OrdinalIgnoreCase))
        {
            settings.LastConnectedDeviceId = null;
            settings.LastConnectedAtUtc = null;
            changed = true;
        }

        foreach (var slot in ActionSlots.All)
        {
            if (!settings.Actions.ContainsKey(slot))
            {
                settings.Actions[slot] = slot == ActionSlots.BoardButton
                    ? new ActionBinding { Kind = ActionKind.None }
                    : new ActionBinding();
                changed = true;
            }
        }

        if (!rawJson.Contains("\"OutputMode\"", StringComparison.Ordinal))
        {
            settings.OutputMode = settings.EnableVJoy && settings.DisableKeyboardActions
                ? OutputMode.VJoy
                : OutputMode.Keyboard;
            changed = true;
        }

        if (!rawJson.Contains("\"JumpVJoyButton\"", StringComparison.Ordinal) && settings.JumpVJoyButton < 1)
        {
            settings.JumpVJoyButton = 1;
            changed = true;
        }

        return changed;
    }
}
