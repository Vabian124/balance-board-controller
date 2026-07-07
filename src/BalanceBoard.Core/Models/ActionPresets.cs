namespace BalanceBoard.Core.Models;

public static class ActionPresets
{
    public const string GameController = "Game Controller";
    public const string Pedal = "Pedal / Rudder";
    public const string KeyboardMovement = "Hand-Free Desktop";
    public const string BalanceMouse = "Balance Mouse";
    public const string Default = "Default";

    public static IReadOnlyList<string> All { get; } =
    [
        Default,
        GameController,
        Pedal,
        KeyboardMovement,
        BalanceMouse,
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
            case BalanceMouse:
                ApplyBalanceMouse(settings);
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
        TriggerDefaults.ApplyTo(settings);
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
        TriggerDefaults.ApplyTo(settings);
        settings.Actions = CreateLegacyKeyboardBindings();
    }

    public static void ApplyBalanceMouse(AppSettings settings)
    {
        settings.ActiveProfileName = BalanceMouse;
        settings.EnableVJoy = false;
        settings.DisableKeyboardActions = false;
        settings.SendCenterOfGravityToAxes = false;
        settings.SendLoadSensorsToAxes = false;
        SensitivityPresets.Apply(settings, SensitivityLevel.Medium);
        settings.Actions = CreateBalanceMouseBindings();
    }

    public static Dictionary<string, ActionBinding> CreateLegacyKeyboardBindings() =>
        new()
        {
            [ActionSlots.Left] = BindKey("A"),
            [ActionSlots.Right] = BindKey("D"),
            [ActionSlots.Forward] = BindKey("W"),
            [ActionSlots.Backward] = BindKey("S"),
            [ActionSlots.Modifier] = BindKey("LShiftKey"),
            [ActionSlots.Jump] = BindMouseButton("Left"),
            [ActionSlots.DiagonalLeft] = BindMouseMoveX(-15),
            [ActionSlots.DiagonalRight] = BindMouseMoveX(15),
        };

    public static Dictionary<string, ActionBinding> CreateBalanceMouseBindings() =>
        new()
        {
            [ActionSlots.Left] = BindMouseMoveX(-12),
            [ActionSlots.Right] = BindMouseMoveX(12),
            [ActionSlots.Forward] = BindMouseMoveY(-12),
            [ActionSlots.Backward] = BindMouseMoveY(12),
            [ActionSlots.Jump] = BindMouseButton("Left"),
            [ActionSlots.Modifier] = new ActionBinding(),
            [ActionSlots.DiagonalLeft] = new ActionBinding(),
            [ActionSlots.DiagonalRight] = new ActionBinding(),
        };

    private static ActionBinding BindKey(string key) =>
        new() { Kind = ActionKind.Key, KeyName = key };

    private static ActionBinding BindMouseButton(string button) =>
        new() { Kind = ActionKind.MouseButton, MouseButton = button };

    private static ActionBinding BindMouseMoveX(int amount) =>
        new() { Kind = ActionKind.MouseMoveX, Amount = amount };

    private static ActionBinding BindMouseMoveY(int amount) =>
        new() { Kind = ActionKind.MouseMoveY, Amount = amount };
}
