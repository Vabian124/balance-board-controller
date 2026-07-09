using BalanceBoard.Core.Abstractions;
using BalanceBoard.Core.Models;
using BalanceBoard.Core.Services.Diagnostics;

namespace BalanceBoard.Core.Services;

/// <summary>
/// Applies output profiles/presets and syncs vJoy init/shutdown onto the ConnectionWorker thread.
/// </summary>
internal sealed class ProfileCoordinator(
    ConnectionWorker worker,
    IGameControllerOutput vjoy,
    Action<string>? log = null)
{

    /// <summary>
    /// Marshals vJoy init/shutdown onto the ConnectionWorker thread. Poll() calls
    /// _vjoy.Update()/Center() inline on that same thread — without this, a settings
    /// save from the UI thread (e.g. toggling "Enable vJoy" or changing the device id)
    /// could Shutdown()/Initialize() the vJoy device concurrently with an in-flight
    /// Update() call on the worker thread.
    /// </summary>
    public void SyncVJoyFromSettingsThreadSafe(AppSettings settings)
    {
        if (worker.IsCurrentThreadWorker)
        {
            SyncVJoyFromSettings(settings);
            return;
        }

        // #region agent log
        AgentDebugLog.Write("H6", "ProfileCoordinator.SyncVJoyFromSettingsThreadSafe", "non-blocking vJoy enqueue", new
        {
            settings.EnableVJoy,
            settings.VJoyDeviceId,
        });
        // #endregion
        worker.Enqueue(() => SyncVJoyFromSettings(settings));
    }

    public void SyncVJoyFromSettings(AppSettings settings)
    {
        if (settings.EnableVJoy)
        {
            vjoy.Initialize(settings.VJoyDeviceId);
        }
        else
        {
            vjoy.Shutdown();
        }
    }

    public void ApplyProfile(AppSettings settings, string profileName)
    {
        ActionPresets.Apply(settings, profileName);
        SyncVJoyFromSettingsThreadSafe(settings);
        log?.Invoke($"Applied profile: {profileName}");
    }

    public void ApplyPreset(AppSettings settings, Action<AppSettings> apply, string logMessage)
    {
        apply(settings);
        SyncVJoyFromSettingsThreadSafe(settings);
        log?.Invoke(logMessage);
    }
}
