using System.Reflection;

namespace BalanceBoard.Core.Models;

public sealed class AppSettings
{
    public int TriggerLeftRight { get; set; } = TriggerDefaults.LeftRight;
    public int TriggerForwardBackward { get; set; } = TriggerDefaults.ForwardBackward;
    public int TriggerModifierLeftRight { get; set; } = TriggerDefaults.ModifierLeftRight;
    public int TriggerModifierForwardBackward { get; set; } = TriggerDefaults.ModifierForwardBackward;
    public bool EnableVJoy { get; set; } = true;
    public bool SendCenterOfGravityToAxes { get; set; } = true;
    public bool SendLoadSensorsToAxes { get; set; }
    public bool DisableKeyboardActions { get; set; } = true;
    /// <summary>When true, one-foot jump detection drives vJoy button 1 (gamepad A).</summary>
    public bool MapJumpToVJoyButton { get; set; }
    public uint VJoyDeviceId { get; set; } = 1;
    public bool AutoConnectOnStartup { get; set; } = true;
    public bool HasConnectedBefore { get; set; }
    public string? LastConnectedDeviceId { get; set; }
    public DateTime? LastConnectedAtUtc { get; set; }
    /// <summary>Host Bluetooth adapter MAC (12 hex, no separators) used for Wii PIN pairing.</summary>
    public string? LastBluetoothAdapterMac { get; set; }
    public bool AutoTareOnConnect { get; set; } = true;
    public bool StartMinimized { get; set; }
    public double DeadzonePercent { get; set; } = 5;
    /// <summary>Per-axis deadzone; null = use <see cref="DeadzonePercent"/>.</summary>
    public double? DeadzoneLeftRightPercent { get; set; }
    public double? DeadzoneForwardBackwardPercent { get; set; }
    public double Sensitivity { get; set; } = 1.0;
    /// <summary>Per-axis stick gain; null = split mode off or use <see cref="Sensitivity"/>.</summary>
    public double? SensitivityLeftRight { get; set; }
    public double? SensitivityForwardBackward { get; set; }
    public SensitivityLevel SensitivityLevel { get; set; } = SensitivityLevel.Medium;
    public bool UseSimpleSensitivity { get; set; } = true;
    public ResponseCurve ResponseCurve { get; set; } = ResponseCurve.Linear;
    public bool OneFootMode { get; set; }
    public float JumpWeightThresholdKg { get; set; } = BalanceConstants.JumpWeightThresholdKg;
    public double JumpHoldSeconds { get; set; } = BalanceConstants.JumpHoldSeconds;
    public JumpLevel JumpLevel { get; set; } = JumpLevel.Normal;
    public UiDetailLevel UiDetailLevel { get; set; } = UiDetailLevel.Standard;
    /// <summary>When true, the pinned session log expander is open.</summary>
    public bool SessionLogExpanded { get; set; }
    public bool InvertX { get; set; }
    public bool InvertY { get; set; }
    /// <summary>vJoy X (left/right) stays centered — forward/back only.</summary>
    public bool LockLeftRightAxis { get; set; }
    /// <summary>vJoy Y (forward/back) stays centered — strafe only.</summary>
    public bool LockForwardBackwardAxis { get; set; }
    public ThemePreference ThemePreference { get; set; } = ThemePreference.System;
    public string ActiveProfileName { get; set; } = "Default";
    public bool SetupWizardCompleted { get; set; }
    public Dictionary<string, ActionBinding> Actions { get; set; } = CreateDefaultActions();

    public static Dictionary<string, ActionBinding> CreateDefaultActions() =>
        ActionSlots.All.ToDictionary(name => name, _ => new ActionBinding());

    /// <summary>
    /// Machine/connection identity fields that must never travel with an exported or shared profile.
    /// </summary>
    private static readonly HashSet<string> ConnectionStateProperties = new(StringComparer.Ordinal)
    {
        nameof(HasConnectedBefore),
        nameof(SetupWizardCompleted),
        nameof(LastConnectedDeviceId),
        nameof(LastConnectedAtUtc),
        nameof(LastBluetoothAdapterMac),
    };

    /// <summary>Reset connection identity so a snapshot can be shared between machines/users.</summary>
    public void ClearConnectionState()
    {
        HasConnectedBefore = false;
        SetupWizardCompleted = false;
        LastConnectedDeviceId = null;
        LastConnectedAtUtc = null;
        LastBluetoothAdapterMac = null;
    }

    /// <summary>
    /// Copy every user-tunable setting from <paramref name="source"/> onto this instance. Uses reflection so
    /// new settings are picked up automatically. Connection identity is preserved unless
    /// <paramref name="includeConnectionState"/> is true. <see cref="Actions"/> is always deep-copied so the two
    /// instances never share <see cref="ActionBinding"/> references.
    /// </summary>
    public void CopyFrom(AppSettings source, bool includeConnectionState = false)
    {
        ArgumentNullException.ThrowIfNull(source);

        foreach (var prop in typeof(AppSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanRead || !prop.CanWrite || prop.GetIndexParameters().Length > 0)
            {
                continue;
            }

            if (prop.Name == nameof(Actions))
            {
                continue;
            }

            if (!includeConnectionState && ConnectionStateProperties.Contains(prop.Name))
            {
                continue;
            }

            prop.SetValue(this, prop.GetValue(source));
        }

        Actions = source.Actions.ToDictionary(
            kv => kv.Key,
            kv => new ActionBinding
            {
                Kind = kv.Value.Kind,
                KeyName = kv.Value.KeyName,
                MouseButton = kv.Value.MouseButton,
                Amount = kv.Value.Amount,
            },
            StringComparer.Ordinal);
    }

    /// <summary>Create a full deep copy, including connection identity.</summary>
    public AppSettings Clone()
    {
        var copy = new AppSettings();
        copy.CopyFrom(this, includeConnectionState: true);
        return copy;
    }
}

public enum ActionKind
{
    None,
    Key,
    MouseButton,
    MouseMoveX,
    MouseMoveY,
}

public sealed class ActionBinding
{
    public ActionKind Kind { get; set; } = ActionKind.None;
    public string KeyName { get; set; } = string.Empty;
    public string MouseButton { get; set; } = string.Empty;
    public int Amount { get; set; } = 10;
}
