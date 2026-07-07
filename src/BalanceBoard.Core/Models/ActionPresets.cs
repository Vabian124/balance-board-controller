namespace BalanceBoard.Core.Models;

public static class ActionPresets
{
    public const string GameController = "Game Controller";
    public const string Pedal = "Pedal / Rudder";
    public const string KeyboardMovement = "Hand-Free Desktop";
    public const string Default = "Default";

    public static IReadOnlyList<string> All { get; } =
    [
        Default,
        GameController,
        Pedal,
        KeyboardMovement,
    ];

    public static void Apply(AppSettings settings, string presetName)
    {
        switch (presetName)
        {
            case GameController:
                ApplyGameController(settings);
                break;
            case Pedal:
                ApplyPedal(settings);
                break;
            case KeyboardMovement:
                ApplyKeyboardMovement(settings);
                break;
            default:
                settings.ActiveProfileName = Default;
                break;
        }
    }

    public static void ApplyGameController(AppSettings settings)
    {
        settings.ActiveProfileName = GameController;
        settings.EnableVJoy = true;
        settings.DisableKeyboardActions = true;
        settings.SendCenterOfGravityToAxes = true;
        settings.SendLoadSensorsToAxes = false;
        settings.TriggerLeftRight = 8;
        settings.TriggerForwardBackward = 9;
        settings.TriggerModifierLeftRight = 15;
        settings.TriggerModifierForwardBackward = 16;
    }

    public static void ApplyPedal(AppSettings settings)
    {
        settings.ActiveProfileName = Pedal;
        settings.EnableVJoy = true;
        settings.DisableKeyboardActions = true;
        settings.SendCenterOfGravityToAxes = false;
        settings.SendLoadSensorsToAxes = true;
    }

    public static void ApplyKeyboardMovement(AppSettings settings)
    {
        settings.ActiveProfileName = KeyboardMovement;
        settings.EnableVJoy = false;
        settings.DisableKeyboardActions = false;
        settings.SendCenterOfGravityToAxes = false;
        settings.SendLoadSensorsToAxes = false;
        settings.TriggerLeftRight = 8;
        settings.TriggerForwardBackward = 9;
        settings.TriggerModifierLeftRight = 15;
        settings.TriggerModifierForwardBackward = 16;
        settings.Actions = CreateLegacyKeyboardBindings();
    }

    public static Dictionary<string, ActionBinding> CreateLegacyKeyboardBindings()
    {
        return new Dictionary<string, ActionBinding>
        {
            ["Left"] = BindKey("A"),
            ["Right"] = BindKey("D"),
            ["Forward"] = BindKey("W"),
            ["Backward"] = BindKey("S"),
            ["Modifier"] = BindKey("LShiftKey"),
            ["Jump"] = BindKey("Space"),
            ["DiagonalLeft"] = BindMouseMoveX(-15),
            ["DiagonalRight"] = BindMouseMoveX(15),
        };
    }

    private static ActionBinding BindKey(string key) =>
        new() { Kind = ActionKind.Key, KeyName = key };

    private static ActionBinding BindMouseMoveX(int amount) =>
        new() { Kind = ActionKind.MouseMoveX, Amount = amount };
}
