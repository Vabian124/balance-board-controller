using BalanceBoard.Core.Models;
using BalanceBoard.Core.Processing;
using Xunit;

namespace BalanceBoard.Core.Tests.Models;

public class VirtualKeyCodesTests
{
    [Theory]
    [InlineData("A", 0x41)]
    [InlineData("w", 0x57)]
    [InlineData("Space", 0x20)]
    [InlineData("LShiftKey", 0xA0)]
    public void TryGet_resolves_preset_keys(string name, ushort expected)
    {
        Assert.True(VirtualKeyCodes.TryGet(name, out var vk));
        Assert.Equal(expected, vk);
    }
}
