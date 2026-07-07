using BalanceBoard.Core.Services;
using Xunit;

namespace BalanceBoard.Core.Tests;

public class VJoyAxisMappingTests
{
    [Theory]
    [InlineData(0, 0, 32767, 16384)]
    [InlineData(0, -32768, 32767, 0)]
    [InlineData(32767, 0, 32767, 32767)]
    [InlineData(-32767, 0, 32767, 0)]
    public void SignedToDevice_maps_center_and_extremes(short signed, long min, long max, long expected)
    {
        Assert.Equal(expected, VJoyAxisMapping.SignedToDevice(signed, min, max));
    }
}
