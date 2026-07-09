namespace BalanceBoard.Core.Models;

public static class ActionPresets
{
    public const string GameController = "Game Controller";
    public const string Pedal = "Pedal / Rudder";
    public const string KeyboardMovement = "Hand-Free Desktop";
    public const string BalanceMouse = "Balance Mouse";
    public const string Minecraft = "Minecraft";
    public const string MinecraftControlify = "Minecraft (Controlify)";
    public const string Default = "Default";

    public static IReadOnlyList<string> All { get; } =
    [
        Default,
        Minecraft,
        GameController,
        MinecraftControlify,
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
            case Minecraft:
                ApplyMinecraft(settings);
                break;
            case MinecraftControlify:
                ApplyMinecraftControlify(settings);
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
        settings.SetVirtualControllerBackend(VirtualControllerBackend.VJoy);
        settings.SetOutputMode(OutputMode.VirtualController);
        settings.SendCenterOfGravityToAxes = true;
        settings.SendLoadSensorsToAxes = false;
        settings.MapJumpToVJoyButton = false;
        settings.Actions[ActionSlots.BoardButton] = new ActionBinding
        {
            Kind = ActionKind.VJoyButton,
            VJoyButtonNumber = 2,
        };
        TriggerDefaults.ApplyTo(settings);
    }

    /// <summary>
    /// Keyboard WASD movement + Space jump — default Minecraft template.
    /// </summary>
    public static void ApplyMinecraft(AppSettings settings)
    {
        settings.ActiveProfileName = Minecraft;
        settings.SetOutputMode(OutputMode.Keyboard);
        settings.SendCenterOfGravityToAxes = false;
        settings.SendLoadSensorsToAxes = false;
        settings.MapJumpToVJoyButton = false;
        JumpPresets.Apply(settings, JumpLevel.Normal);
        SensitivityPresets.Apply(settings, SensitivityLevel.Medium);
        settings.ResponseCurve = ResponseCurve.MinecraftSnappy;
        TriggerDefaults.ApplyTo(settings);
        settings.Actions = CreateMinecraftKeyboardBindings();
        settings.Actions[ActionSlots.BoardButton] = new ActionBinding { Kind = ActionKind.Key, KeyName = "Escape" };
    }

    /// <summary>
    /// vJoy left stick = move (lean), button 1 = jump — bind vJoy Device 1 in Controlify for Minecraft.
    /// </summary>
    public static void ApplyMinecraftControlify(AppSettings settings)
    {
        settings.ActiveProfileName = MinecraftControlify;
        settings.SetVirtualControllerBackend(VirtualControllerBackend.VJoy);
        settings.SetOutputMode(OutputMode.VirtualController);
        settings.SendCenterOfGravityToAxes = true;
        settings.SendLoadSensorsToAxes = false;
        settings.MapJumpToVJoyButton = true;
        settings.JumpVJoyButton = 1;
        JumpPresets.Apply(settings, JumpLevel.Normal);
        SensitivityPresets.Apply(settings, SensitivityLevel.Medium);
        settings.ResponseCurve = ResponseCurve.MinecraftSnappy;
        TriggerDefaults.ApplyTo(settings);
        settings.Actions[ActionSlots.BoardButton] = new ActionBinding
        {
            Kind = ActionKind.VJoyButton,
            VJoyButtonNumber = 2,
        };
    }

    public static void ApplyPedal(AppSettings settings)
    {
        settings.ActiveProfileName = Pedal;
        settings.SetVirtualControllerBackend(VirtualControllerBackend.VJoy);
        settings.SetOutputMode(OutputMode.VirtualController);
        settings.SendCenterOfGravityToAxes = false;
        settings.SendLoadSensorsToAxes = true;
    }

    public static void ApplyKeyboardMovement(AppSettings settings)
    {
        settings.ActiveProfileName = KeyboardMovement;
        settings.SetOutputMode(OutputMode.Keyboard);
        settings.SendCenterOfGravityToAxes = false;
        settings.SendLoadSensorsToAxes = false;
        TriggerDefaults.ApplyTo(settings);
        settings.Actions = CreateLegacyKeyboardBindings();
        settings.Actions[ActionSlots.BoardButton] = new ActionBinding { Kind = ActionKind.Key, KeyName = "Escape" };
    }

    public static void ApplyBalanceMouse(AppSettings settings)
    {
        settings.ActiveProfileName = BalanceMouse;
        settings.SetOutputMode(OutputMode.Keyboard);
        settings.SendCenterOfGravityToAxes = false;
        settings.SendLoadSensorsToAxes = false;
        SensitivityPresets.Apply(settings, SensitivityLevel.Medium);
        settings.Actions = CreateBalanceMouseBindings();
        settings.Actions[ActionSlots.BoardButton] = new ActionBinding { Kind = ActionKind.None };
    }

    public static Dictionary<string, ActionBinding> CreateMinecraftKeyboardBindings() =>
        new()
        {
            [ActionSlots.Left] = BindKey("A"),
            [ActionSlots.Right] = BindKey("D"),
            [ActionSlots.Forward] = BindKey("W"),
            [ActionSlots.Backward] = BindKey("S"),
            [ActionSlots.Jump] = BindKey("Space"),
            [ActionSlots.Modifier] = new ActionBinding(),
            [ActionSlots.DiagonalLeft] = new ActionBinding(),
            [ActionSlots.DiagonalRight] = new ActionBinding(),
        };

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
