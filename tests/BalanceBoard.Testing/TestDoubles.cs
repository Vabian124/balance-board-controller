using BalanceBoard.Core.Abstractions;
using BalanceBoard.Core.Models;

namespace BalanceBoard.Testing;

public sealed class NullGameControllerOutput : IGameControllerOutput
{
    public bool IsReady => false;

    public bool Initialize(uint deviceId = 1, bool attemptCleanupOnBusy = true) => false;

    public void Update(ProcessedBalance data, AppSettings settings)
    {
    }

    public void Center()
    {
    }

    public void Shutdown()
    {
    }

    public void Dispose()
    {
    }
}

/// <summary>
/// Records the managed thread id of every call so tests can assert vJoy init/shutdown
/// never races with Update/Center calls made from the ConnectionWorker poll loop.
/// </summary>
public sealed class ThreadTrackingGameControllerOutput : IGameControllerOutput
{
    private readonly object _sync = new();
    private bool _acquired;

    public List<(string Call, int ThreadId)> Calls { get; } = [];

    public bool IsReady => _acquired;

    public bool Initialize(uint deviceId = 1, bool attemptCleanupOnBusy = true)
    {
        lock (_sync)
        {
            Calls.Add(("Initialize", Environment.CurrentManagedThreadId));
            _acquired = true;
            return true;
        }
    }

    public void Update(ProcessedBalance data, AppSettings settings)
    {
        lock (_sync)
        {
            Calls.Add(("Update", Environment.CurrentManagedThreadId));
        }
    }

    public void Center()
    {
        lock (_sync)
        {
            Calls.Add(("Center", Environment.CurrentManagedThreadId));
        }
    }

    public void Shutdown()
    {
        lock (_sync)
        {
            Calls.Add(("Shutdown", Environment.CurrentManagedThreadId));
            _acquired = false;
        }
    }

    public void Dispose() => Shutdown();
}

public sealed class NullActionSimulator : IActionSimulator
{
    public void Apply(ProcessedBalance data, AppSettings settings)
    {
    }

    public void ReleaseAll()
    {
    }
}

/// <summary>
/// Records every keyboard/mouse call made through the action engine for
/// end-to-end assertions without touching the real Win32 SendInput API.
/// </summary>
public sealed class RecordingInputBackend : IInputBackend
{
    private readonly object _sync = new();

    public List<(string Kind, ushort VirtualKey, string Button, int DeltaX, int DeltaY)> Events { get; } = [];

    public void KeyDown(ushort virtualKey)
    {
        lock (_sync)
        {
            Events.Add(("keydown", virtualKey, string.Empty, 0, 0));
        }
    }

    public void KeyUp(ushort virtualKey)
    {
        lock (_sync)
        {
            Events.Add(("keyup", virtualKey, string.Empty, 0, 0));
        }
    }

    public void MouseDown(string button)
    {
        lock (_sync)
        {
            Events.Add(("mousedown", 0, button, 0, 0));
        }
    }

    public void MouseUp(string button)
    {
        lock (_sync)
        {
            Events.Add(("mouseup", 0, button, 0, 0));
        }
    }

    public void MoveRelative(int deltaX, int deltaY)
    {
        lock (_sync)
        {
            Events.Add(("move", 0, string.Empty, deltaX, deltaY));
        }
    }

    public IReadOnlyList<(string Kind, ushort VirtualKey, string Button, int DeltaX, int DeltaY)> Snapshot()
    {
        lock (_sync)
        {
            return [.. Events];
        }
    }
}
