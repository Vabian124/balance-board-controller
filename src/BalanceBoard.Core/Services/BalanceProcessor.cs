using BalanceBoard.Core.Models;
using BalanceBoard.Core.Processing;

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
        var highest = BalanceMath.HighestCornerKg(reading);
        _offsetTopLeft = highest - reading.TopLeftKg;
        _offsetTopRight = highest - reading.TopRightKg;
        _offsetBottomLeft = highest - reading.BottomLeftKg;
        _offsetBottomRight = highest - reading.BottomRightKg;
        _resetCenterPossible = true;
    }

    public bool CanSetCenter(float weightKg) => weightKg > BalanceConstants.WeightOnBoardThresholdKg;

    public bool CanResetCenter(float weightKg) =>
        weightKg <= BalanceConstants.WeightOnBoardThresholdKg && _resetCenterPossible;

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
        UpdateMinCorner(reading);

        var (topLeft, topRight, bottomLeft, bottomRight, weight) = BalanceMath.NormalizeCorners(
            reading,
            _minCorner,
            _offsetTopLeft,
            _offsetTopRight,
            _offsetBottomLeft,
            _offsetBottomRight);

        var (owrTopLeft, owrTopRight, owrBottomLeft, owrBottomRight, total) =
            BalanceMath.ToBalancePercent(topLeft, topRight, bottomLeft, bottomRight);

        var (balanceX, balanceY) = BalanceMath.ComputeBalanceXY(
            owrTopLeft, owrTopRight, owrBottomLeft, owrBottomRight);

        var (moveLeft, moveRight, moveForward, moveBackward) =
            BalanceMath.EvaluateCardinalMovement(balanceX, balanceY, settings);

        var modifier = BalanceMath.EvaluateModifier(balanceX, balanceY, settings);
        var jump = BalanceMath.EvaluateJump(weight, DateTime.UtcNow, ref _jumpTime);

        var (diagonalLeft, diagonalRight) = BalanceMath.EvaluateDiagonals(
            total,
            bottomLeft,
            topRight,
            bottomRight,
            topLeft,
            moveLeft,
            moveRight,
            moveForward,
            moveBackward);

        var (joyX, joyY) = BalanceMath.MapCenterOfGravityAxes(balanceX, balanceY, settings);
        var (joyZ, joyRx, joyRy, joyRz) = BalanceMath.MapLoadSensorAxes(reading, settings);

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
            JoyZ = joyZ,
            JoyRx = joyRx,
            JoyRy = joyRy,
            JoyRz = joyRz,
            ButtonA = reading.ButtonA,
        };
    }

    private void UpdateMinCorner(BalanceReading reading)
    {
        if (reading.TopLeftKg < _minCorner)
        {
            _minCorner = reading.TopLeftKg;
        }

        if (reading.TopRightKg < _minCorner)
        {
            _minCorner = reading.TopRightKg;
        }

        if (reading.BottomLeftKg < _minCorner)
        {
            _minCorner = reading.BottomLeftKg;
        }

        if (reading.BottomRightKg < _minCorner)
        {
            _minCorner = reading.BottomRightKg;
        }
    }
}
