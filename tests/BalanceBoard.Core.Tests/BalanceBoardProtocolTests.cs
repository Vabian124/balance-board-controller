using BalanceBoard.Core.Services;
using Xunit;

namespace BalanceBoard.Core.Tests;

public class BalanceBoardProtocolTests
{
    [Fact]
    public void Extension_ids_match_WiiBrew_balance_board()
    {
        Assert.Equal(0x0402, BalanceBoardProtocol.ExtensionEncryptedId);
        Assert.Equal(0x2A2C, BalanceBoardProtocol.ExtensionDecryptedId);
        Assert.Equal(0x34, BalanceBoardProtocol.WeightReportId);
        Assert.Contains("12 04 34", BalanceBoardProtocol.ContinuousReportModeHex, StringComparison.Ordinal);
    }

    [Fact]
    public void WiimoteLib_balance_board_enum_encrypted_half_word_is_0402()
    {
        const ulong balanceBoardRaw = 0x00000000A4200402UL;
        var encrypted = (ushort)(balanceBoardRaw & 0xFFFF);
        Assert.Equal(BalanceBoardProtocol.ExtensionEncryptedId, encrypted);
    }
}
