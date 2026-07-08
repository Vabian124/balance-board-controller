using BalanceBoard.Core.Abstractions;
using BalanceBoard.Core.Models;

namespace BalanceBoard.Core.Services;

/// <summary>
/// Applies output profiles/presets and syncs vJoy init/shutdown onto the ConnectionWorker thread.
/// </summary>
internal sealed class ProfileCoordinator
{
    private readonly ConnectionWorker _worker;
    private readonly IGameControllerOutput _vjoy;
    private readonly Action<string>? _log;

    public ProfileCoordinator(
        ConnectionWorker worker,
        IGameControllerOutput vjoy,
        Action<string>? log = null)
    {
        _worker = worker;
        _vjoy = vjoy;
        _log = log;
    }

    /// <summary>
    /// Marshals vJoy init/shutdown onto the ConnectionWorker thread. Poll() calls
    /// _vjoy.Update()/Center() inline on that same thread — without this, a settings
    /// save from the UI thread (e.g. toggling "Enable vJoy" or changing the device id)
    /// could Shutdown()/Initialize() the vJoy device concurrently with an in-flight
    /// Update() call on the worker thread.
    /// </summary>
    public void SyncVJoyFromSettingsThreadSafe(AppSettings settings) =>
        _worker.Invoke(() => SyncVJoyFromSettings(settings));

    public void SyncVJoyFromSettings(AppSettings settings)
    {
        if (settings.EnableVJoy)
        {
            _vjoy.Initialize(settings.VJoyDeviceId);
        }
        else
        {
            _vjoy.Shutdown();
        }
    }

    public void ApplyProfile(AppSettings settings, string profileName)
    {
        ActionPresets.Apply(settings, profileName);
        SyncVJoyFromSettingsThreadSafe(settings);
        _log?.Invoke($"Applied profile: {profileName}");
    }

    public void ApplyPreset(AppSettings settings, Action<AppSettings> apply, string logMessage)
    {
        apply(settings);
        SyncVJoyFromSettingsThreadSafe(settings);
        _log?.Invoke(logMessage);
    }
}
