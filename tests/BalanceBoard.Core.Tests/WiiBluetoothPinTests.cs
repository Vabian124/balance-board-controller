using BalanceBoard.Core.Services.Connection;
using Xunit;

namespace BalanceBoard.Core.Tests;

/// <summary>Permanent Wii PIN matches WiiBalanceWalker FormBluetooth.AddressToWiiPin / WiiBrew host-MAC-reversed protocol.</summary>
public class WiiBluetoothPinTests
{
    [Fact]
    public void TryCreateFromHostMac_reverses_bytes_like_reference()
    {
        Assert.True(WiiBluetoothPin.TryCreateFromHostMac("A841F4E6F034", out var pin, out var error));
        Assert.Null(error);
        Assert.Equal(6, pin.Length);
        Assert.Equal((char)0x34, pin[0]);
        Assert.Equal((char)0xF0, pin[1]);
        Assert.Equal((char)0xE6, pin[2]);
        Assert.Equal((char)0xF4, pin[3]);
        Assert.Equal((char)0x41, pin[4]);
        Assert.Equal((char)0xA8, pin[5]);
    }

    [Fact]
    public void TryCreateFromHostMac_rejects_mac_with_double_zero()
    {
        Assert.False(WiiBluetoothPin.TryCreateFromHostMac("001A70007113", out _, out var error));
        Assert.Contains("00", error!, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatMacForDisplay_adds_colons()
    {
        Assert.Equal("A8:41:F4:E6:F0:34", WiiBluetoothPin.FormatMacForDisplay("A841F4E6F034"));
    }
}
