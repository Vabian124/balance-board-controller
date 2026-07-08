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

    /// <summary>
    /// Reconstructs the ABSOLUTE gross weight on the board.
    /// WiimoteLib applies a software zero-point (tare): once set, it subtracts the captured
    /// baseline from every corner and therefore from <c>WeightKg</c>. That baseline is meant to
    /// be the empty board, but a tare taken while someone is standing on the board (e.g. auto-tare
    /// during startup auto-connect) captures their body weight — leaving the reported total weight
    /// stuck near zero. Adding the zero-point total back yields the true weight on the board for
    /// display, independent of when the tare happened. Lean/balance still use the relative
    /// (zeroed) corner distribution, so tare keeps working for centering.
    /// </summary>
    public static float RestoreAbsoluteWeightKg(float taredWeightKg, bool zeroPointSet, float zeroPointTotalKg) =>
        zeroPointSet ? taredWeightKg + zeroPointTotalKg : taredWeightKg;

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
        float owrBottomRight)
    {
        var balanceX = owrBottomRight + owrTopRight;
        var balanceY = owrBottomRight + owrBottomLeft;
        if (balanceX < BalanceConstants.MinTotalWeightEpsilon
            && balanceY < BalanceConstants.MinTotalWeightEpsilon)
        {
            return (BalanceConstants.BalanceCenterPercent, BalanceConstants.BalanceCenterPercent);
        }

        return (balanceX, balanceY);
    }

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
        ref DateTime jumpTime,
        ref bool aboveJumpThreshold)
    {
        if (weightKg < jumpThresholdKg)
        {
            if (aboveJumpThreshold)
            {
                jumpTime = utcNow;
                aboveJumpThreshold = false;
            }

            return !aboveJumpThreshold && utcNow.Subtract(jumpTime).TotalSeconds < jumpHoldSeconds;
        }

        aboveJumpThreshold = true;
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

        var deadzoneX = ResolveDeadzonePercent(settings.DeadzoneLeftRightPercent, settings.DeadzonePercent);
        var deadzoneY = ResolveDeadzonePercent(settings.DeadzoneForwardBackwardPercent, settings.DeadzonePercent);
        var (x, y) = ApplyEllipticalDeadzone(balanceX, balanceY, deadzoneX, deadzoneY);

        var sensX = ResolveSensitivity(settings.SensitivityLeftRight, settings.Sensitivity);
        var sensY = ResolveSensitivity(settings.SensitivityForwardBackward, settings.Sensitivity);

        var joyX = settings.LockLeftRightAxis
            ? (short)0
            : MapLeanToJoyAxis(x, sensX, settings.InvertX, settings.ResponseCurve);
        var joyY = settings.LockForwardBackwardAxis
            ? (short)0
            : MapLeanToJoyAxis(y, sensY, settings.InvertY, settings.ResponseCurve);

        return (joyX, joyY);
    }

    /// <summary>Elliptical deadzone — diagonal leans keep full range (fixes corner-only stick output).</summary>
    public static (float X, float Y) ApplyEllipticalDeadzone(
        float balanceX,
        float balanceY,
        double deadzoneX,
        double deadzoneY)
    {
        var center = BalanceConstants.BalanceCenterPercent;
        if (deadzoneX <= 0 && deadzoneY <= 0)
        {
            return (balanceX, balanceY);
        }

        if (deadzoneX <= 0)
        {
            return (balanceX, ApplyDeadzone(balanceY, deadzoneY));
        }

        if (deadzoneY <= 0)
        {
            return (ApplyDeadzone(balanceX, deadzoneX), balanceY);
        }

        var dx = balanceX - center;
        var dy = balanceY - center;
        var dzx = Math.Max(deadzoneX, 0.001);
        var dzy = Math.Max(deadzoneY, 0.001);
        var ex = dx / dzx;
        var ey = dy / dzy;
        var len = Math.Sqrt(ex * ex + ey * ey);
        if (len <= 1.0)
        {
            return (center, center);
        }

        var maxLen = Math.Sqrt(
            center / dzx * (center / dzx) +
            center / dzy * (center / dzy));
        var t = Math.Min(1.0, (len - 1.0) / Math.Max(maxLen - 1.0, 1e-6));
        var outEx = ex / len * t * maxLen;
        var outEy = ey / len * t * maxLen;
        return ((float)(center + outEx * dzx), (float)(center + outEy * dzy));
    }

    public static double ResolveDeadzonePercent(double? axisValue, double fallback) =>
        axisValue ?? fallback;

    public static double ResolveSensitivity(double? axisValue, double fallback) =>
        axisValue is > 0 ? axisValue.Value : fallback;

    /// <summary>
    /// Maps balance percent (0–100, center 50) to a vJoy axis.
    /// Sensitivity is “gain”: higher values need less lean for full stick (e.g. 10× ≈ 5% lean → max).
    /// </summary>
    public static short MapLeanToJoyAxis(float balancePercent, double sensitivity, bool invert, ResponseCurve curve)
    {
        var center = BalanceConstants.BalanceCenterPercent;
        var delta = balancePercent - center;
        if (Math.Abs(delta) < 1e-6f)
        {
            return 0;
        }

        var throwAt = center / Math.Max(0.25, sensitivity);
        var t = Math.Clamp(Math.Abs(delta) / throwAt, 0, 1);
        if (curve != ResponseCurve.Linear)
        {
            t = SensitivityCurve.Map((float)t, curve);
        }

        var magnitude = (int)(t * short.MaxValue);
        var sign = invert ? -1 : 1;
        return (short)(Math.Sign(delta) * sign * magnitude);
    }

    public static (short Z, short Rx, short Ry, short Rz) MapLoadSensorAxes(BalanceReading reading, AppSettings settings)
    {
        if (!settings.SendLoadSensorsToAxes)
        {
            return (0, 0, 0, 0);
        }

        var mul = BalanceConstants.SensorKgToAxisMultiplier;
        return (
            ClampToAxis(reading.TopLeftKg * mul),
            ClampToAxis(reading.TopRightKg * mul),
            ClampToAxis(reading.BottomLeftKg * mul),
            ClampToAxis(reading.BottomRightKg * mul));
    }

    /// <summary>
    /// Clamps before the narrowing cast to <see cref="short"/> — a noisy/glitched HID reading
    /// (or an unusually heavy corner load) can exceed +-327.67 kg * multiplier, which would
    /// otherwise silently wrap around to a wildly wrong (possibly opposite-sign) axis value.
    /// </summary>
    private static short ClampToAxis(float value)
    {
        if (float.IsNaN(value))
        {
            return 0;
        }

        return (short)Math.Clamp(value, short.MinValue, short.MaxValue);
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
