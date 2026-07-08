using BalanceBoard.Core.Models;
using BalanceBoard.Core.Processing;
using BalanceBoard.Core.Services;
using Xunit;

namespace BalanceBoard.Core.Tests;

public class UserReportedFixesTests
{
    [Theory]
    [InlineData(5f, true, 70f, 75f)]
    [InlineData(0f, true, 62f, 62f)]
    [InlineData(10f, false, 0f, 10f)]
    public void RestoreAbsoluteWeightKg_reconstructs_gross_weight(
        float taredWeightKg,
        bool zeroPointSet,
        float zeroPointTotalKg,
        float expectedKg)
    {
        var absolute = BalanceMath.RestoreAbsoluteWeightKg(taredWeightKg, zeroPointSet, zeroPointTotalKg);
        Assert.Equal(expectedKg, absolute, precision: 3);
    }

    [Theory]
    [InlineData(ActionSlots.Forward, 50f, 30f, 0.4f)]
    [InlineData(ActionSlots.Forward, 50f, 50f, 0f)]
    [InlineData(ActionSlots.Left, 30f, 50f, 0.4f)]
    [InlineData(ActionSlots.Jump, 30f, 30f, 1f)]
    public void SlotIntensity_scales_with_lean_magnitude(string slot, float balanceX, float balanceY, float expected)
    {
        var data = new ProcessedBalance { BalanceX = balanceX, BalanceY = balanceY };
        var intensity = MovementMapper.SlotIntensity(slot, data);
        Assert.Equal(expected, intensity, precision: 2);
    }

    [Theory]
    [InlineData(20, 0.1f, 2)]
    [InlineData(20, 0.5f, 10)]
    [InlineData(20, 1f, 20)]
    [InlineData(-20, 0.25f, -5)]
    [InlineData(20, 0.01f, 1)]
    public void ScaleMouseAmount_preserves_direction_and_minimum_step(int amount, float intensity, int expected)
    {
        Assert.Equal(expected, MovementMapper.ScaleMouseAmount(amount, intensity));
    }

    [Theory]
    [InlineData("LeftShift", 0xA0)]
    [InlineData("D1", 0x31)]
    [InlineData("W", 0x57)]
    public void VirtualKeyCodes_resolves_wpf_capture_names(string keyName, ushort expectedVk)
    {
        Assert.True(VirtualKeyCodes.TryGet(keyName, out var vk));
        Assert.Equal(expectedVk, vk);
    }

    [Fact]
    public void VJoyConfigLocator_returns_first_existing_candidate()
    {
        var candidates = VJoyConfigLocator.CandidatePaths();
        Assert.NotEmpty(candidates);
        var chosen = candidates[1];
        var found = VJoyConfigLocator.FindConfigExe(path => string.Equals(path, chosen, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(chosen, found);
    }
}
