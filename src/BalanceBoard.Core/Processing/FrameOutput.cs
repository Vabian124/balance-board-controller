using BalanceBoard.Core.Abstractions;
using BalanceBoard.Core.Models;

namespace BalanceBoard.Core.Processing;

/// <summary>
/// Applies processed balance data to vJoy and keyboard/mouse outputs using <see cref="OutputRoutingPolicy"/>.
/// </summary>
public static class FrameOutput
{
    public static void Apply(
        ProcessedBalance processed,
        AppSettings settings,
        IGameControllerOutput vjoy,
        IActionSimulator input,
        Action<string>? log = null)
    {
        if (OutputRoutingPolicy.ShouldSendVJoy(settings, vjoy.IsReady))
        {
            try
            {
                vjoy.Update(processed, settings);
            }
            catch (Exception ex)
            {
                log?.Invoke($"[VJOY] Output error: {ex.Message}");
                log?.Invoke(ex.StackTrace ?? string.Empty);
            }
        }

        if (OutputRoutingPolicy.ShouldInvokeInputSimulator(settings))
        {
            try
            {
                input.Apply(processed, settings);
            }
            catch (Exception ex)
            {
                log?.Invoke($"Input simulator error: {ex.Message}");
                log?.Invoke(ex.StackTrace ?? string.Empty);
            }
        }
    }
}
