using BalanceBoard.Core.Models;

namespace BalanceBoard.Core.Processing;

/// <summary>
/// Pure balance-board math (no state, no I/O). Port to Python <c>balance_math.py</c> line-for-line.
/// </summary>
public static class BalanceMath
{
    public static float HighestCornerKg(BalanceReading reading) =>
        Math.Max(
            Math.Max(reading.TopLeftKg, reading.TopRightKg),
            Math.Max(reading.BottomLeftKg, reading.BottomRightKg));

    public static (float TopLeft, float TopRight, float BottomLeft, float BottomRight, float Weight) NormalizeCorners(
        BalanceReading reading,
        float minCorner,
        float offsetTopLeft,
        float offsetTopRight,
        float offsetBottomLeft,
        float offsetBottomRight)
    {
        var weight = reading.WeightKg < 0 ? 0 : reading.WeightKg;
        var topLeft = reading.TopLeftKg - minCorner;
        var topRight = reading.TopRightKg - minCorner;
        var bottomLeft = reading.BottomLeftKg - minCorner;
        var bottomRight = reading.BottomRightKg - minCorner;

        if (weight > BalanceConstants.WeightOnBoardThresholdKg)
        {
            topLeft += offsetTopLeft;
            topRight += offsetTopRight;
            bottomLeft += offsetBottomLeft;
            bottomRight += offsetBottomRight;
        }
        else
        {
            topLeft = 0;
            topRight = 0;
            bottomLeft = 0;
            bottomRight = 0;
        }

        return (topLeft, topRight, bottomLeft, bottomRight, weight);
    }

    public static (float TopLeft, float TopRight, float BottomLeft, float BottomRight, float Total) ToBalancePercent(
        float topLeft,
        float topRight,
        float bottomLeft,
        float bottomRight)
    {
        var total = topLeft + topRight + bottomLeft + bottomRight;
        if (total <= BalanceConstants.MinTotalWeightEpsilon)
        {
            return (0, 0, 0, 0, 0);
        }

        var scale = BalanceConstants.PercentScale / total;
        return (
            scale * topLeft,
            scale * topRight,
            scale * bottomLeft,
            scale * bottomRight,
            total);
    }

    public static (float BalanceX, float BalanceY) ComputeBalanceXY(
        float owrTopLeft,
        float owrTopRight,
        float owrBottomLeft,
        float owrBottomRight) =>
        (owrBottomRight + owrTopRight, owrBottomRight + owrBottomLeft);

    public static (bool MoveLeft, bool MoveRight, bool MoveForward, bool MoveBackward) EvaluateCardinalMovement(
        float balanceX,
        float balanceY,
        AppSettings settings)
    {
        var center = BalanceConstants.BalanceCenterPercent;
        return (
            balanceX < center - settings.TriggerLeftRight,
            balanceX > center + settings.TriggerLeftRight,
            balanceY < center - settings.TriggerForwardBackward,
            balanceY > center + settings.TriggerForwardBackward);
    }

    public static bool EvaluateModifier(float balanceX, float balanceY, AppSettings settings)
    {
        var center = BalanceConstants.BalanceCenterPercent;
        return balanceX < center - settings.TriggerModifierLeftRight
            || balanceX > center + settings.TriggerModifierLeftRight
            || balanceY < center - settings.TriggerModifierForwardBackward
            || balanceY > center + settings.TriggerModifierForwardBackward;
    }

    public static bool EvaluateJump(
        float weightKg,
        float jumpThresholdKg,
        double jumpHoldSeconds,
        DateTime utcNow,
        ref DateTime jumpTime)
    {
        if (weightKg < jumpThresholdKg)
        {
            return utcNow.Subtract(jumpTime).TotalSeconds < jumpHoldSeconds;
        }

        jumpTime = utcNow;
        return false;
    }

    public static (bool DiagonalLeft, bool DiagonalRight) EvaluateDiagonals(
        float total,
        float bottomLeft,
        float topRight,
        float bottomRight,
        float topLeft,
        bool moveLeft,
        bool moveRight,
        bool moveForward,
        bool moveBackward)
    {
        if (moveLeft || moveRight || moveForward || moveBackward || total <= BalanceConstants.MinTotalWeightEpsilon)
        {
            return (false, false);
        }

        var scale = BalanceConstants.PercentScale / total;
        var brDl = scale * (bottomLeft + topRight);
        var brDr = scale * (bottomRight + topLeft);
        var brDf = Math.Abs(brDl - brDr);

        if (brDf <= BalanceConstants.DiagonalDeltaThreshold)
        {
            return (false, false);
        }

        return brDl > brDr ? (true, false) : (false, true);
    }

    public static (short JoyX, short JoyY) MapCenterOfGravityAxes(
        float balanceX,
        float balanceY,
        AppSettings settings,
        bool onBoard = true)
    {
        if (!settings.SendCenterOfGravityToAxes || !onBoard)
        {
            return (0, 0);
        }

        var x = balanceX;
        var y = balanceY;
        if (settings.DeadzonePercent > 0)
        {
            x = ApplyDeadzone(x, settings.DeadzonePercent);
            y = ApplyDeadzone(y, settings.DeadzonePercent);
        }

        return (
            ToJoyAxis(x, settings.Sensitivity, settings.InvertX),
            ToJoyAxis(y, settings.Sensitivity, settings.InvertY));
    }

    public static (short Z, short Rx, short Ry, short Rz) MapLoadSensorAxes(BalanceReading reading, AppSettings settings)
    {
        if (!settings.SendLoadSensorsToAxes)
        {
            return (0, 0, 0, 0);
        }

        var mul = BalanceConstants.SensorKgToAxisMultiplier;
        return (
            (short)(reading.TopLeftKg * mul),
            (short)(reading.TopRightKg * mul),
            (short)(reading.BottomLeftKg * mul),
            (short)(reading.BottomRightKg * mul));
    }

    public static float ApplyDeadzone(float percent, double deadzone)
    {
        var delta = percent - BalanceConstants.BalanceCenterPercent;
        if (Math.Abs(delta) < deadzone)
        {
            return BalanceConstants.BalanceCenterPercent;
        }

        var sign = Math.Sign(delta);
        var scaled = (Math.Abs(delta) - deadzone) / (BalanceConstants.BalanceCenterPercent - deadzone)
            * BalanceConstants.BalanceCenterPercent;
        return (float)(BalanceConstants.BalanceCenterPercent + sign * scaled);
    }

    public static short ToJoyAxis(float percent, double sensitivity, bool invert)
    {
        double value = percent * BalanceConstants.JoyAxisScale + BalanceConstants.JoyAxisOffset;
        value *= BalanceConstants.JoySensitivityMultiplier * sensitivity;
        if (invert)
        {
            value *= -1;
        }

        if (double.IsNaN(value))
        {
            return 0;
        }

        return (short)Math.Clamp(value, short.MinValue, short.MaxValue);
    }
}
