using BalanceBoard.Core.Services;
using Xunit;

namespace BalanceBoard.Core.Tests;

public class ExceptionLogFormatterTests
{
    [Fact]
    public void IsBenignDeviceIo_detects_object_disposed_and_thread_pool_handle()
    {
        Assert.True(ExceptionLogFormatter.IsBenignDeviceIo(new ObjectDisposedException("x")));
        Assert.True(ExceptionLogFormatter.IsBenignDeviceIo(
            new InvalidOperationException("ThreadPoolBoundHandle already disposed")));
        Assert.False(ExceptionLogFormatter.IsBenignDeviceIo(new InvalidOperationException("real failure")));
    }

    [Fact]
    public void FormatHeader_includes_context_tags_and_hresult()
    {
        var ex = new IOException("device gone") { HResult = unchecked((int)0x8007001F) };
        var header = ExceptionLogFormatter.FormatHeader(ex, "HID read", [LogTags.Hid, LogTags.Connect]);

        Assert.Contains("context=HID read", header);
        Assert.Contains("tags=[HID,CONNECT]", header);
        Assert.Contains("IOException", header);
        Assert.Contains("HRESULT=0x8007001F", header);
        Assert.Contains("tid=", header);
    }
}
