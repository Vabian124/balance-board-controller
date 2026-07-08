using BalanceBoard.Core.Abstractions;
using BalanceBoard.Core.Models;
using BalanceBoard.Core.Processing;
using Xunit;

namespace BalanceBoard.Core.Tests.Processing;

public class FrameOutputTests
{
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void ShouldSendVJoy_matches_enable_and_ready_flags(bool enableVJoy, bool vJoyReady, bool expected)
    {
        var settings = new AppSettings { EnableVJoy = enableVJoy };
        Assert.Equal(expected, OutputRoutingPolicy.ShouldSendVJoy(settings, vJoyReady));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void ShouldSendKeyboardMovement_inverts_disable_keyboard_flag(bool disableKeyboard, bool expected)
    {
        var settings = new AppSettings { DisableKeyboardActions = disableKeyboard };
        Assert.Equal(expected, OutputRoutingPolicy.ShouldSendKeyboardMovement(settings));
    }

    [Theory]
    [InlineData(OutputMode.Keyboard, ActionKind.Key, "Escape", true)]
    [InlineData(OutputMode.Keyboard, ActionKind.None, "", true)]
    [InlineData(OutputMode.VJoy, ActionKind.Key, "Escape", true)]
    [InlineData(OutputMode.VJoy, ActionKind.Key, "", false)]
    [InlineData(OutputMode.VJoy, ActionKind.None, "", false)]
    [InlineData(OutputMode.VJoy, ActionKind.MouseButton, "Left", false)]
    [InlineData(OutputMode.VJoy, ActionKind.VJoyButton, "", false)]
    public void ShouldInvokeInputSimulator_matrix(
        OutputMode mode,
        ActionKind boardButtonKind,
        string boardButtonKey,
        bool expected)
    {
        var settings = CreateSettings(mode, boardButtonKind, boardButtonKey);
        Assert.Equal(expected, OutputRoutingPolicy.ShouldInvokeInputSimulator(settings));
    }

    [Theory]
    [InlineData(ActionKind.Key, "Escape", true)]
    [InlineData(ActionKind.Key, " ", false)]
    [InlineData(ActionKind.Key, "", false)]
    [InlineData(ActionKind.None, "", false)]
    [InlineData(ActionKind.MouseButton, "Left", false)]
    public void HasBoardButtonKeyBinding_requires_key_kind_and_non_blank_name(
        ActionKind kind,
        string keyName,
        bool expected)
    {
        var settings = new AppSettings();
        settings.Actions[ActionSlots.BoardButton] = new ActionBinding { Kind = kind, KeyName = keyName };
        Assert.Equal(expected, OutputRoutingPolicy.HasBoardButtonKeyBinding(settings));
    }

    [Theory]
    [InlineData(OutputMode.VJoy, true, ActionKind.Key, "Escape", true, true)]
    [InlineData(OutputMode.VJoy, false, ActionKind.Key, "Escape", false, true)]
    [InlineData(OutputMode.VJoy, true, ActionKind.None, "", true, false)]
    [InlineData(OutputMode.Keyboard, false, ActionKind.Key, "Escape", false, true)]
    public void Apply_routes_vjoy_and_input_per_policy(
        OutputMode mode,
        bool vJoyReady,
        ActionKind boardButtonKind,
        string boardButtonKey,
        bool expectVJoyUpdate,
        bool expectInputApply)
    {
        var settings = CreateSettings(mode, boardButtonKind, boardButtonKey);
        var processed = new ProcessedBalance();
        var vjoy = new RecordingGameControllerOutput { IsReady = vJoyReady };
        var input = new RecordingActionSimulator();

        FrameOutput.Apply(processed, settings, vjoy, input);

        Assert.Equal(expectVJoyUpdate, vjoy.UpdateCount == 1);
        Assert.Equal(expectInputApply, input.ApplyCount == 1);
    }

    [Fact]
    public void Apply_vjoy_error_is_logged_and_input_still_runs()
    {
        var settings = new AppSettings { EnableVJoy = true, DisableKeyboardActions = false };
        var processed = new ProcessedBalance();
        var vjoy = new ThrowingGameControllerOutput { IsReady = true };
        var input = new RecordingActionSimulator();
        var log = new List<string>();

        FrameOutput.Apply(processed, settings, vjoy, input, log.Add);

        Assert.Contains(log, line => line.StartsWith("[VJOY] Output error:", StringComparison.Ordinal));
        Assert.Equal(1, input.ApplyCount);
    }

    [Fact]
    public void Apply_input_error_is_logged_without_throwing()
    {
        var settings = new AppSettings();
        settings.SetOutputMode(OutputMode.Keyboard);
        var processed = new ProcessedBalance();
        var vjoy = new RecordingGameControllerOutput { IsReady = false };
        var input = new ThrowingActionSimulator();
        var log = new List<string>();

        FrameOutput.Apply(processed, settings, vjoy, input, log.Add);

        Assert.Contains(log, line => line.StartsWith("Input simulator error:", StringComparison.Ordinal));
    }

    private static AppSettings CreateSettings(OutputMode mode, ActionKind boardButtonKind, string boardButtonKey)
    {
        var settings = new AppSettings();
        settings.SetOutputMode(mode);
        settings.Actions[ActionSlots.BoardButton] = boardButtonKind switch
        {
            ActionKind.MouseButton => new ActionBinding { Kind = ActionKind.MouseButton, MouseButton = boardButtonKey },
            ActionKind.VJoyButton => new ActionBinding { Kind = ActionKind.VJoyButton, VJoyButtonNumber = 1 },
            _ => new ActionBinding { Kind = boardButtonKind, KeyName = boardButtonKey },
        };
        return settings;
    }

    private sealed class RecordingGameControllerOutput : IGameControllerOutput
    {
        public bool IsReady { get; init; }
        public int UpdateCount { get; private set; }

        public void Update(ProcessedBalance data, AppSettings settings) => UpdateCount++;

        public bool Initialize(uint deviceId = 1, bool attemptCleanupOnBusy = true) => true;

        public void Center() { }

        public void Shutdown() { }

        public void Dispose() { }
    }

    private sealed class ThrowingGameControllerOutput : IGameControllerOutput
    {
        public bool IsReady { get; init; }

        public void Update(ProcessedBalance data, AppSettings settings) =>
            throw new InvalidOperationException("vJoy failed");

        public bool Initialize(uint deviceId = 1, bool attemptCleanupOnBusy = true) => true;

        public void Center() { }

        public void Shutdown() { }

        public void Dispose() { }
    }

    private sealed class RecordingActionSimulator : IActionSimulator
    {
        public int ApplyCount { get; private set; }

        public void Apply(ProcessedBalance data, AppSettings settings) => ApplyCount++;

        public void ReleaseAll() { }
    }

    private sealed class ThrowingActionSimulator : IActionSimulator
    {
        public void Apply(ProcessedBalance data, AppSettings settings) =>
            throw new InvalidOperationException("input failed");

        public void ReleaseAll() { }
    }
}
