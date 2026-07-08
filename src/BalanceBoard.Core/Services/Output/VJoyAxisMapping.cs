namespace BalanceBoard.Core.Services.Output;

/// <summary>Maps internal signed stick values (0 = center) to vJoy device axis ranges.</summary>
public static class VJoyAxisMapping
{
    public static long SignedToDevice(short signed, long axisMin, long axisMax)
    {
        if (axisMax <= axisMin)
        {
            return axisMin;
        }

        var center = (axisMin + axisMax) / 2.0;
        var halfRange = (axisMax - axisMin) / 2.0;
        var device = center + signed / (double)short.MaxValue * halfRange;
        return (long)Math.Clamp(Math.Round(device), axisMin, axisMax);
    }
}
