using BalanceBoard.Core.Models;

namespace BalanceBoard.Core.Processing;

/// <summary>
/// Pure routing rules for per-frame output (vJoy vs keyboard/mouse). Mirrors the legacy
/// <c>OnReading</c> gate including the BoardButton key exception in vJoy mode.
/// </summary>
public static class OutputRoutingPolicy
{
    public static bool ShouldSendVJoy(AppSettings settings, bool vJoyIsReady) =>
        settings.EnableVJoy && vJoyIsReady;

    public static bool ShouldSendKeyboardMovement(AppSettings settings) =>
        !settings.DisableKeyboardActions;

    public static bool ShouldInvokeInputSimulator(AppSettings settings) =>
        ShouldSendKeyboardMovement(settings) || HasBoardButtonKeyBinding(settings);

    public static bool HasBoardButtonKeyBinding(AppSettings settings) =>
        settings.Actions.TryGetValue(ActionSlots.BoardButton, out var binding)
        && binding.Kind == ActionKind.Key
        && !string.IsNullOrWhiteSpace(binding.KeyName);
}
