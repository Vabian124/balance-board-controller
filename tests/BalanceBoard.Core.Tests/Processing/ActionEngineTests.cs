using BalanceBoard.Core.Abstractions;
using BalanceBoard.Core.Models;
using BalanceBoard.Core.Processing;
using BalanceBoard.Core.Services;
using BalanceBoard.Testing;
using Xunit;

namespace BalanceBoard.Core.Tests.Processing;

public class ActionEngineTests
{
    [Fact]
    public void Apply_presses_key_for_active_slot()
    {
        var backend = new RecordingInputBackend();
        var engine = new ActionEngine(backend);
        var settings = new AppSettings { Actions = AppSettings.CreateDefaultActions() };
        settings.Actions[ActionSlots.Left] = new() { Kind = ActionKind.Key, KeyName = "A" };

        var data = new ProcessedBalance { MoveLeft = true };
        engine.Apply(data, settings);

        Assert.Contains(backend.Events, e => e.Kind == "keydown" && e.VirtualKey == 0x41);
    }

    [Fact]
    public void Apply_presses_mouse_on_jump()
    {
        var backend = new RecordingInputBackend();
        var engine = new ActionEngine(backend);
        var settings = new AppSettings { Actions = AppSettings.CreateDefaultActions() };
        settings.Actions[ActionSlots.Jump] = new() { Kind = ActionKind.MouseButton, MouseButton = "Left" };

        engine.Apply(new ProcessedBalance { Jump = true }, settings);

        Assert.Contains(backend.Events, e => e.Kind == "mousedown");
    }

    [Fact]
    public void ReleaseAll_releases_held_keys()
    {
        var backend = new RecordingInputBackend();
        var engine = new ActionEngine(backend);
        var settings = new AppSettings { Actions = AppSettings.CreateDefaultActions() };
        settings.Actions[ActionSlots.Forward] = new() { Kind = ActionKind.Key, KeyName = "W" };

        engine.Apply(new ProcessedBalance { MoveForward = true }, settings);
        engine.ReleaseAll();

        Assert.Contains(backend.Events, e => e.Kind == "keyup" && e.VirtualKey == 0x57);
    }

    [Fact]
    public void Apply_rebinding_active_slot_releases_old_key_before_pressing_new_key()
    {
        var backend = new RecordingInputBackend();
        var engine = new ActionEngine(backend);
        var settings = new AppSettings { Actions = AppSettings.CreateDefaultActions() };
        settings.Actions[ActionSlots.Left] = new() { Kind = ActionKind.Key, KeyName = "A" };

        engine.Apply(new ProcessedBalance { MoveLeft = true }, settings);
        Assert.Single(backend.Events, e => e.Kind == "keydown" && e.VirtualKey == 0x41);

        settings.Actions[ActionSlots.Left] = new() { Kind = ActionKind.Key, KeyName = "B" };
        engine.Apply(new ProcessedBalance { MoveLeft = true }, settings);

        Assert.Single(backend.Events, e => e.Kind == "keyup" && e.VirtualKey == 0x41);
        Assert.Single(backend.Events, e => e.Kind == "keydown" && e.VirtualKey == 0x42);

        engine.ReleaseAll();

        Assert.Single(backend.Events, e => e.Kind == "keyup" && e.VirtualKey == 0x41);
        Assert.Single(backend.Events, e => e.Kind == "keyup" && e.VirtualKey == 0x42);
    }

    [Fact]
    public void Apply_rebinding_amount_only_does_not_toggle_mouse_move_timer()
    {
        var backend = new RecordingInputBackend();
        var engine = new ActionEngine(backend);
        var settings = new AppSettings { Actions = AppSettings.CreateDefaultActions() };
        settings.Actions[ActionSlots.DiagonalLeft] = new() { Kind = ActionKind.MouseMoveX, Amount = 10 };

        engine.Apply(new ProcessedBalance { DiagonalLeft = true }, settings);
        settings.Actions[ActionSlots.DiagonalLeft] = new() { Kind = ActionKind.MouseMoveX, Amount = 20 };
        engine.Apply(new ProcessedBalance { DiagonalLeft = true }, settings);

        Assert.DoesNotContain(backend.Events, e => e.Kind is "keydown" or "keyup" or "mousedown" or "mouseup");

        engine.ReleaseAll();
    }
}
