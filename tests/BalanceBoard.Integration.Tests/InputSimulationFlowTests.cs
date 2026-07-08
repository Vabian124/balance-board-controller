using BalanceBoard.Core.Models;
using BalanceBoard.Core.Services;
using BalanceBoard.Testing;
using Xunit;

namespace BalanceBoard.Integration.Tests;

/// <summary>
/// End-to-end verification of the balance board -> keyboard/mouse path: fake HID reading ->
/// <see cref="BalanceProcessor"/> -> <see cref="BalanceBoardSession"/> -> real <see cref="InputSimulator"/>
/// (production <c>ActionEngine</c> + <c>MovementMapper</c>) -> recorded backend calls. No production
/// code is mocked except the lowest-level Win32 SendInput call, so this proves the wiring that a
/// physical board relies on without requiring real hardware.
/// </summary>
public class InputSimulationFlowTests
{
    private static readonly BalanceReading LeftLean = new()
    {
        WeightKg = 60,
        TopLeftKg = 25,
        TopRightKg = 5,
        BottomLeftKg = 25,
        BottomRightKg = 5,
        IsBalanceBoard = true,
    };

    private static readonly BalanceReading ForwardLean = new()
    {
        WeightKg = 60,
        TopLeftKg = 25,
        TopRightKg = 25,
        BottomLeftKg = 5,
        BottomRightKg = 5,
        IsBalanceBoard = true,
    };

    private static BalanceBoardSession CreateSession(
        RecordingInputBackend backend,
        FakeBalanceBoardConnection connection,
        AppSettings settings)
    {
        var session = new BalanceBoardSession(
            gameController: new NullGameControllerOutput(),
            actionSimulator: new InputSimulator(backend),
            connection: connection,
            pairing: new FakeBluetoothPairingService());
        session.LoadSettings(settings, initializeVJoy: false);
        return session;
    }

    [Fact]
    public async Task Left_lean_sends_key_down_when_keyboard_actions_enabled()
    {
        var backend = new RecordingInputBackend();
        var connection = new FakeBalanceBoardConnection { NextReading = LeftLean };
        var settings = new AppSettings { EnableVJoy = false, DisableKeyboardActions = false, AutoTareOnConnect = false };
        ActionPresets.ApplyKeyboardMovement(settings);

        using var session = CreateSession(backend, connection, settings);
        var result = await session.ConnectWithIntentAsync(ConnectionIntent.QuickReconnect);
        Assert.True(result.IsSuccess);

        await Task.Delay(300);

        var events = backend.Snapshot();
        Assert.Contains(events, e => e.Kind == "keydown" && e.VirtualKey == 0x41); // 'A' = Left binding
    }

    [Fact]
    public async Task Forward_W_binding_sends_key_down_when_keyboard_actions_enabled()
    {
        // Regression: user sets Forward=W in Advanced but DisableKeyboardActions (from vJoy preset)
        // prevented any key injection unless explicitly re-enabled.
        var backend = new RecordingInputBackend();
        var connection = new FakeBalanceBoardConnection { NextReading = ForwardLean };
        var settings = new AppSettings
        {
            EnableVJoy = false,
            DisableKeyboardActions = false,
            AutoTareOnConnect = false,
            Actions = AppSettings.CreateDefaultActions(),
        };
        settings.Actions[ActionSlots.Forward] = new() { Kind = ActionKind.Key, KeyName = "W" };

        using var session = CreateSession(backend, connection, settings);
        var result = await session.ConnectWithIntentAsync(ConnectionIntent.QuickReconnect);
        Assert.True(result.IsSuccess);

        await Task.Delay(300);

        var events = backend.Snapshot();
        Assert.Contains(events, e => e.Kind == "keydown" && e.VirtualKey == 0x57);
    }

    [Fact]
    public async Task Disable_keyboard_actions_suppresses_all_backend_calls()
    {
        var backend = new RecordingInputBackend();
        var connection = new FakeBalanceBoardConnection { NextReading = LeftLean };
        var settings = new AppSettings { EnableVJoy = false, DisableKeyboardActions = true, AutoTareOnConnect = false };
        ActionPresets.ApplyKeyboardMovement(settings);
        settings.DisableKeyboardActions = true; // preset above forces it back off; re-assert the gate under test

        using var session = CreateSession(backend, connection, settings);
        var result = await session.ConnectWithIntentAsync(ConnectionIntent.QuickReconnect);
        Assert.True(result.IsSuccess);

        await Task.Delay(300);

        Assert.Empty(backend.Snapshot());
    }

    [Fact]
    public async Task Jump_sends_mouse_down_for_balance_mouse_preset()
    {
        var backend = new RecordingInputBackend();
        var airborne = new BalanceReading
        {
            WeightKg = 0.2f,
            TopLeftKg = 0.2f,
            TopRightKg = 0.2f,
            BottomLeftKg = 0.2f,
            BottomRightKg = 0.2f,
            IsBalanceBoard = true,
        };
        var connection = new FakeBalanceBoardConnection { NextReading = LeftLean };
        var settings = new AppSettings
        {
            EnableVJoy = false,
            DisableKeyboardActions = false,
            AutoTareOnConnect = false,
            JumpWeightThresholdKg = 1f,
            JumpHoldSeconds = 0.05,
        };
        ActionPresets.ApplyBalanceMouse(settings);
        settings.JumpWeightThresholdKg = 1f;
        settings.JumpHoldSeconds = 0.05;

        using var session = CreateSession(backend, connection, settings);
        var result = await session.ConnectWithIntentAsync(ConnectionIntent.QuickReconnect);
        Assert.True(result.IsSuccess);

        await Task.Delay(150);
        connection.NextReading = airborne;
        await Task.Delay(300);

        var events = backend.Snapshot();
        Assert.Contains(events, e => e.Kind == "mousedown" && e.Button == "Left");
    }

    [Fact]
    public async Task Disconnect_releases_held_keys()
    {
        var backend = new RecordingInputBackend();
        var connection = new FakeBalanceBoardConnection { NextReading = LeftLean };
        var settings = new AppSettings { EnableVJoy = false, DisableKeyboardActions = false, AutoTareOnConnect = false };
        ActionPresets.ApplyKeyboardMovement(settings);

        using var session = CreateSession(backend, connection, settings);
        await session.ConnectWithIntentAsync(ConnectionIntent.QuickReconnect);
        await Task.Delay(300);
        Assert.Contains(backend.Snapshot(), e => e.Kind == "keydown" && e.VirtualKey == 0x41);

        session.Disconnect();

        Assert.Contains(backend.Snapshot(), e => e.Kind == "keyup" && e.VirtualKey == 0x41);
    }
}
