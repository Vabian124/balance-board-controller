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
    public uint VJoyDeviceId { get; set; } = 1;
    public bool AutoConnectOnStartup { get; set; } = true;
    public bool HasConnectedBefore { get; set; }
    public string? LastConnectedDeviceId { get; set; }
    public DateTime? LastConnectedAtUtc { get; set; }
    public bool AutoTareOnConnect { get; set; } = true;
    public bool StartMinimized { get; set; }
    public double DeadzonePercent { get; set; } = 5;
    public double Sensitivity { get; set; } = 1.0;
    public bool InvertX { get; set; }
    public bool InvertY { get; set; }
    public string ActiveProfileName { get; set; } = "Default";
    public bool SetupWizardCompleted { get; set; }
    public Dictionary<string, ActionBinding> Actions { get; set; } = CreateDefaultActions();

    public static Dictionary<string, ActionBinding> CreateDefaultActions() =>
        ActionSlots.All.ToDictionary(name => name, _ => new ActionBinding());
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
