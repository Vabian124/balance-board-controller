namespace BalanceBoard.Core.Models;

public sealed class AppSettings
{
    public int TriggerLeftRight { get; set; } = 8;
    public int TriggerForwardBackward { get; set; } = 9;
    public int TriggerModifierLeftRight { get; set; } = 15;
    public int TriggerModifierForwardBackward { get; set; } = 16;
    public bool EnableVJoy { get; set; } = true;
    public bool SendCenterOfGravityToAxes { get; set; } = true;
    public bool SendLoadSensorsToAxes { get; set; }
    public bool DisableKeyboardActions { get; set; } = true;
    public uint VJoyDeviceId { get; set; } = 1;
    public bool AutoConnectOnStartup { get; set; }
    public bool AutoTareOnConnect { get; set; } = true;
    public bool StartMinimized { get; set; }
    public double DeadzonePercent { get; set; } = 5;
    public double Sensitivity { get; set; } = 1.0;
    public bool InvertX { get; set; }
    public bool InvertY { get; set; }
    public string ActiveProfileName { get; set; } = "Default";
    public Dictionary<string, ActionBinding> Actions { get; set; } = CreateDefaultActions();

    public static Dictionary<string, ActionBinding> CreateDefaultActions()
    {
        return new Dictionary<string, ActionBinding>
        {
            ["Left"] = new() { Kind = ActionKind.None },
            ["Right"] = new() { Kind = ActionKind.None },
            ["Forward"] = new() { Kind = ActionKind.None },
            ["Backward"] = new() { Kind = ActionKind.None },
            ["Modifier"] = new() { Kind = ActionKind.None },
            ["Jump"] = new() { Kind = ActionKind.None },
            ["DiagonalLeft"] = new() { Kind = ActionKind.None },
            ["DiagonalRight"] = new() { Kind = ActionKind.None },
        };
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
