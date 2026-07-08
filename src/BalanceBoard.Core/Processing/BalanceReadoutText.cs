using BalanceBoard.Core.Models;

namespace BalanceBoard.Core.Processing;

/// <summary>
/// Dashboard readout strings for direction, lean, active inputs, and board button.
/// Shared by WPF MainWindow and unit tests.
/// </summary>
public static class BalanceReadoutText
{
    public static string DescribeDirection(ProcessedBalance data)
    {
        if (data.Jump)
        {
            return data.WeightKg < BalanceConstants.WeightOnBoardThresholdKg
                ? "Jump!"
                : "Jump! · " + DescribeLean(data);
        }

        if (data.WeightKg < BalanceConstants.WeightOnBoardThresholdKg)
        {
            return "Step on the board";
        }

        var lean = DescribeLean(data);
        return string.IsNullOrEmpty(lean) ? "Centered" : lean;
    }

    public static string DescribeLean(ProcessedBalance data)
    {
        var parts = new List<string>();
        if (data.MoveForward)
        {
            parts.Add("forward");
        }

        if (data.MoveBackward)
        {
            parts.Add("backward");
        }

        if (data.MoveLeft)
        {
            parts.Add("left");
        }

        if (data.MoveRight)
        {
            parts.Add("right");
        }

        return string.Join(" · ", parts);
    }

    public static string DescribeActiveInputs(ProcessedBalance data, AppSettings settings)
    {
        if (data.Jump && data.WeightKg < BalanceConstants.WeightOnBoardThresholdKg)
        {
            return "Active: Jump";
        }

        if (data.WeightKg < BalanceConstants.WeightOnBoardThresholdKg)
        {
            return $"Profile: {settings.ActiveProfileName}";
        }

        var active = new List<string>();
        if (data.MoveForward)
        {
            active.Add("Forward");
        }

        if (data.MoveBackward)
        {
            active.Add("Backward");
        }

        if (data.MoveLeft)
        {
            active.Add("Left");
        }

        if (data.MoveRight)
        {
            active.Add("Right");
        }

        if (data.Jump)
        {
            active.Add("Jump");
        }

        return active.Count == 0 ? "Centered" : $"Active: {string.Join(", ", active)}";
    }

    public static string DescribeBoardButton(ProcessedBalance data, AppSettings settings)
    {
        if (!settings.Actions.TryGetValue(ActionSlots.BoardButton, out var binding))
        {
            return data.ButtonA ? "Board button: pressed (A)" : "Board button: up";
        }

        if (data.ButtonA)
        {
            return binding.Kind switch
            {
                ActionKind.Key => $"Board button → {binding.KeyName}",
                ActionKind.VJoyButton => $"Board button → vJoy #{binding.VJoyButtonNumber}",
                _ => "Board button: pressed (A)",
            };
        }

        if (data.Jump && settings.MapJumpToVJoyButton)
        {
            return $"Jump → vJoy #{settings.JumpVJoyButton}";
        }

        return "Board button: up";
    }
}
