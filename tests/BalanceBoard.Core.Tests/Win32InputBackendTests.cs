using BalanceBoard.Core.Models;
using BalanceBoard.Core.Services;
using Xunit;

namespace BalanceBoard.Core.Tests;

public class Win32InputBackendTests
{
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_SCANCODE = 0x0008;

    [Theory]
    [InlineData("Left", true)]
    [InlineData("Right", true)]
    [InlineData("Up", true)]
    [InlineData("Down", true)]
    [InlineData("Home", true)]
    [InlineData("End", true)]
    [InlineData("PageUp", true)]
    [InlineData("PageDown", true)]
    [InlineData("Insert", true)]
    [InlineData("Delete", true)]
    [InlineData("RControlKey", true)]
    [InlineData("RMenu", true)]
    [InlineData("LWin", true)]
    [InlineData("RWin", true)]
    [InlineData("A", false)]
    [InlineData("Space", false)]
    [InlineData("LControlKey", false)]
    [InlineData("LMenu", false)]
    [InlineData("ShiftKey", false)]
    public void RequiresExtendedKeyFlag_matches_known_extended_keys(string keyName, bool expected)
    {
        Assert.True(VirtualKeyCodes.TryGet(keyName, out var vk));
        Assert.Equal(expected, Win32InputBackend.RequiresExtendedKeyFlag(vk));
    }

    [Fact]
    public void BuildKeyEventFlags_sets_extended_bit_for_arrows()
    {
        Assert.True(VirtualKeyCodes.TryGet("Left", out var left));
        var flags = Win32InputBackend.BuildKeyEventFlags(left, keyUp: false);
        Assert.Equal(KEYEVENTF_SCANCODE | KEYEVENTF_EXTENDEDKEY, flags);

        var upFlags = Win32InputBackend.BuildKeyEventFlags(left, keyUp: true);
        Assert.Equal(KEYEVENTF_SCANCODE | KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, upFlags);
    }

    [Fact]
    public void BuildKeyEventFlags_omits_extended_bit_for_letter_keys()
    {
        Assert.True(VirtualKeyCodes.TryGet("W", out var w));
        var flags = Win32InputBackend.BuildKeyEventFlags(w, keyUp: false);
        Assert.Equal(KEYEVENTF_SCANCODE, flags);
        Assert.Equal(0u, flags & KEYEVENTF_EXTENDEDKEY);
    }
}

public class FeederProcessCleanupTests
{
    [Theory]
    [InlineData(@"C:\Games\BalanceBoardApp\BalanceBoardApp.exe", "BalanceBoardApp", true)]
    [InlineData(@"C:\Users\me\source\wiiboardguirevamp\src\BalanceBoard.App\bin\BalanceBoardApp.exe", "BalanceBoardApp", true)]
    [InlineData(@"C:\Tools\WiiBalanceWalker\WiiBalanceWalker.exe", "WiiBalanceWalker", true)]
    [InlineData(@"D:\Apps\WBBGUI\WBBGUI.exe", "WBBGUI", true)]
    [InlineData(@"C:\TotallyUnrelated\BalanceBoardApp.exe", "BalanceBoardApp", true)] // filename match
    [InlineData(@"C:\Unrelated\SomeOtherApp.exe", "BalanceBoardApp", false)]
    [InlineData(null, "BalanceBoardApp", true)] // unreadable → allow (legacy behavior)
    [InlineData("", "BalanceBoardApp", true)]
    public void PathLooksLikeKnownFeeder_matches_expected(string? path, string processName, bool expected) =>
        Assert.Equal(expected, FeederProcessCleanup.PathLooksLikeKnownFeeder(path, processName));
}
