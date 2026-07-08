using BalanceBoard.Core.Services;
using Xunit;

namespace BalanceBoard.Core.Tests;

public class DeviceSelectionTests
{
    [Fact]
    public void IndexOfPreferred_finds_case_insensitive_match()
    {
        IReadOnlyList<string> ids = ["aaa", "BBB", "ccc"];
        Assert.Equal(1, DeviceSelection.IndexOfPreferred(ids, "bbb"));
        Assert.Equal(-1, DeviceSelection.IndexOfPreferred(ids, "zzz"));
        Assert.Equal(-1, DeviceSelection.IndexOfPreferred(ids, null));
    }

    [Fact]
    public void ResolveDeviceIndex_returns_zero_for_single_device()
    {
        Assert.Equal(0, DeviceSelection.ResolveDeviceIndex(["only"], preferredDeviceId: null, allowAmbiguousDefault: false));
    }

    [Fact]
    public void ResolveDeviceIndex_returns_preferred_when_present()
    {
        IReadOnlyList<string> ids = ["board-a", "board-b", "board-c"];
        Assert.Equal(2, DeviceSelection.ResolveDeviceIndex(ids, "board-c", allowAmbiguousDefault: false));
    }

    [Fact]
    public void ResolveDeviceIndex_returns_null_when_ambiguous_and_picker_required()
    {
        IReadOnlyList<string> ids = ["board-a", "board-b"];
        Assert.Null(DeviceSelection.ResolveDeviceIndex(ids, preferredDeviceId: null, allowAmbiguousDefault: false));
        Assert.Null(DeviceSelection.ResolveDeviceIndex(ids, "missing", allowAmbiguousDefault: false));
    }

    [Fact]
    public void ResolveDeviceIndex_defaults_to_zero_when_allowed()
    {
        IReadOnlyList<string> ids = ["board-a", "board-b"];
        Assert.Equal(0, DeviceSelection.ResolveDeviceIndex(ids, preferredDeviceId: null, allowAmbiguousDefault: true));
    }
}
