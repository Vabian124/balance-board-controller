using BalanceBoard.Core.Models;

namespace BalanceBoard.Core.Services;

public sealed class BalanceProcessor
{
    private float _minCorner;
    private float _offsetTopLeft;
    private float _offsetTopRight;
    private float _offsetBottomLeft;
    private float _offsetBottomRight;
    private bool _resetCenterPossible;
    private DateTime _jumpTime = DateTime.UtcNow;

    public void Tare()
    {
        _minCorner = 0;
        _offsetTopLeft = 0;
        _offsetTopRight = 0;
        _offsetBottomLeft = 0;
        _offsetBottomRight = 0;
        _resetCenterPossible = false;
    }

    public void SetCenterFromCurrentReading(BalanceReading reading)
    {
        var highest = Math.Max(
            Math.Max(reading.TopLeftKg, reading.TopRightKg),
            Math.Max(reading.BottomLeftKg, reading.BottomRightKg));

        _offsetTopLeft = highest - reading.TopLeftKg;
        _offsetTopRight = highest - reading.TopRightKg;
        _offsetBottomLeft = highest - reading.BottomLeftKg;
        _offsetBottomRight = highest - reading.BottomRightKg;
        _resetCenterPossible = true;
    }

    public bool CanSetCenter(float weightKg) => weightKg > 5f;

    public bool CanResetCenter(float weightKg) => weightKg <= 5f && _resetCenterPossible;

    public void ResetCenterOffsets()
    {
        _offsetTopLeft = 0;
        _offsetTopRight = 0;
        _offsetBottomLeft = 0;
        _offsetBottomRight = 0;
        _resetCenterPossible = false;
    }

    public ProcessedBalance Process(BalanceReading reading, AppSettings settings)
    {
        var rwTopLeft = reading.TopLeftKg;
        var rwTopRight = reading.TopRightKg;
        var rwBottomLeft = reading.BottomLeftKg;
        var rwBottomRight = reading.BottomRightKg;

        if (rwTopLeft < _minCorner) _minCorner = rwTopLeft;
        if (rwTopRight < _minCorner) _minCorner = rwTopRight;
        if (rwBottomLeft < _minCorner) _minCorner = rwBottomLeft;
        if (rwBottomRight < _minCorner) _minCorner = rwBottomRight;

        var weight = reading.WeightKg < 0 ? 0 : reading.WeightKg;
        var topLeft = rwTopLeft - _minCorner;
        var topRight = rwTopRight - _minCorner;
        var bottomLeft = rwBottomLeft - _minCorner;
        var bottomRight = rwBottomRight - _minCorner;

        if (weight > 5f)
        {
            topLeft += _offsetTopLeft;
            topRight += _offsetTopRight;
            bottomLeft += _offsetBottomLeft;
            bottomRight += _offsetBottomRight;
        }
        else
        {
            topLeft = 0;
            topRight = 0;
            bottomLeft = 0;
            bottomRight = 0;
        }

        var total = topLeft + topRight + bottomLeft + bottomRight;
        float owrTopLeft = 0, owrTopRight = 0, owrBottomLeft = 0, owrBottomRight = 0;
        if (total > 0.001f)
        {
            var scale = 100f / total;
            owrTopLeft = scale * topLeft;
            owrTopRight = scale * topRight;
            owrBottomLeft = scale * bottomLeft;
            owrBottomRight = scale * bottomRight;
        }

        var balanceX = owrBottomRight + owrTopRight;
        var balanceY = owrBottomRight + owrBottomLeft;

        var moveLeft = balanceX < 50f - settings.TriggerLeftRight;
        var moveRight = balanceX > 50f + settings.TriggerLeftRight;
        var moveForward = balanceY < 50f - settings.TriggerForwardBackward;
        var moveBackward = balanceY > 50f + settings.TriggerForwardBackward;

        var modifier = false;
        if (balanceX < 50f - settings.TriggerModifierLeftRight) modifier = true;
        else if (balanceX > 50f + settings.TriggerModifierLeftRight) modifier = true;
        else if (balanceY < 50f - settings.TriggerModifierForwardBackward) modifier = true;
        else if (balanceY > 50f + settings.TriggerModifierForwardBackward) modifier = true;

        var jump = false;
        if (weight < 1f)
        {
            if (DateTime.UtcNow.Subtract(_jumpTime).TotalSeconds < 2) jump = true;
        }
        else
        {
            _jumpTime = DateTime.UtcNow;
        }

        var diagonalLeft = false;
        var diagonalRight = false;
        var brDl = total > 0.001f ? (100f / total) * (bottomLeft + topRight) : 0;
        var brDr = total > 0.001f ? (100f / total) * (bottomRight + topLeft) : 0;
        var brDf = Math.Abs(brDl - brDr);
        if (!moveLeft && !moveRight && !moveForward && !moveBackward && brDf > 15)
        {
            if (brDl > brDr) diagonalLeft = true;
            else diagonalRight = true;
        }

        short joyX = 0;
        short joyY = 0;
        if (settings.SendCenterOfGravityToAxes)
        {
            var x = balanceX;
            var y = balanceY;
            if (settings.DeadzonePercent > 0)
            {
                x = ApplyDeadzone(x, settings.DeadzonePercent);
                y = ApplyDeadzone(y, settings.DeadzonePercent);
            }

            joyX = ToJoyAxis(x, settings.Sensitivity, settings.InvertX);
            joyY = ToJoyAxis(y, settings.Sensitivity, settings.InvertY);
        }

        var sensorTopLeft = settings.SendLoadSensorsToAxes ? (short)(reading.TopLeftKg * 100) : (short)0;
        var sensorTopRight = settings.SendLoadSensorsToAxes ? (short)(reading.TopRightKg * 100) : (short)0;
        var sensorBottomLeft = settings.SendLoadSensorsToAxes ? (short)(reading.BottomLeftKg * 100) : (short)0;
        var sensorBottomRight = settings.SendLoadSensorsToAxes ? (short)(reading.BottomRightKg * 100) : (short)0;

        return new ProcessedBalance
        {
            WeightKg = weight,
            TopLeftKg = topLeft,
            TopRightKg = topRight,
            BottomLeftKg = bottomLeft,
            BottomRightKg = bottomRight,
            BalanceX = balanceX,
            BalanceY = balanceY,
            Jump = jump,
            MoveLeft = moveLeft,
            MoveRight = moveRight,
            MoveForward = moveForward,
            MoveBackward = moveBackward,
            Modifier = modifier,
            DiagonalLeft = diagonalLeft,
            DiagonalRight = diagonalRight,
            JoyX = joyX,
            JoyY = joyY,
            JoyZ = sensorTopLeft,
            JoyRx = sensorTopRight,
            JoyRy = sensorBottomLeft,
            JoyRz = sensorBottomRight,
            ButtonA = reading.ButtonA,
        };
    }

    private static float ApplyDeadzone(float percent, double deadzone)
    {
        var delta = percent - 50f;
        if (Math.Abs(delta) < deadzone) return 50f;
        var sign = Math.Sign(delta);
        var scaled = (Math.Abs(delta) - deadzone) / (50f - deadzone) * 50f;
        return (float)(50f + sign * scaled);
    }

    private static short ToJoyAxis(float percent, double sensitivity, bool invert)
    {
        var value = percent * 655.34 + -32767.0;
        value *= 2.0 * sensitivity;
        if (invert) value *= -1;
        if (double.IsNaN(value)) return 0;
        return (short)Math.Clamp(value, short.MinValue, short.MaxValue);
    }
}
